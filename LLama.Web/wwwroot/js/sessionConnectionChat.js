const createConnectionSessionChat = () => {
    const outputErrorTemplate = $("#outputErrorTemplate").html();
    const outputInfoTemplate = $("#outputInfoTemplate").html();
    const outputUserTemplate = $("#outputUserTemplate").html();
    const outputBotTemplate = $("#outputBotTemplate").html();
    const signatureTemplate = $("#signatureTemplate").html();

    let inferenceSession;
    const connection = new signalR.HubConnectionBuilder().withUrl("/SessionConnectionHub").build();

    const scrollContainer = $("#scroll-container");
    const outputContainer = $("#output-container");
    const chatInput = $("#input");

    // helper to safely extract clean model name
    function normalizeModelName(value) {
        if (!value) return "";
        // take last part of path and strip .gguf
        return value.split("\\").pop().replace(".gguf", "");
    }

    //  STATUS HANDLERS
    const onStatus = (connection, status) => {
        if (status == Enums.SessionConnectionStatus.Disconnected) {
            onError("Socket not connected")
        }
        else if (status == Enums.SessionConnectionStatus.Connected) {
            onInfo("Socket connected")
        }
        else if (status == Enums.SessionConnectionStatus.Loaded) {
            loaderHide();
            enableControls();
            $("#load").hide();
            $("#unload").show();
            onInfo(`New model session successfully started`)
        }
    };

    const onError = (error) => {
        enableControls();
        outputContainer.append(Mustache.render(outputErrorTemplate, { text: error, date: getDateTime() }));
    };

    const onInfo = (message) => {
        outputContainer.append(Mustache.render(outputInfoTemplate, { text: message, date: getDateTime() }));
    };

    let responseContent;
    let responseContainer;
    let responseFirstToken;

    const onResponse = (response) => {
        if (!response) return;

        if (response.tokenType == Enums.TokenType.Begin) {
            let uniqueId = randomString();
            outputContainer.append(Mustache.render(outputBotTemplate, { uniqueId: uniqueId, ...response }));
            responseContainer = $(`#${uniqueId}`);
            responseContent = responseContainer.find(".content");
            responseFirstToken = true;
            scrollToBottom(true);
            return;
        }

        if (response.tokenType == Enums.TokenType.End || response.tokenType == Enums.TokenType.Cancel) {
            enableControls();
            responseContainer.find(".signature").append(Mustache.render(signatureTemplate, response));
            scrollToBottom();
        } else {
            if (responseFirstToken) {
                responseContent.empty();
                responseFirstToken = false;
                responseContainer.find(".date").append(getDateTime());
                responseContent.append(response.content.trim());
            } else {
                responseContent.append(response.content);
            }
            scrollToBottom();
        }
    };

    const sendPrompt = async () => {
        const text = chatInput.val();
        if (!text) return;

        chatInput.val(null);
        disableControls();
        outputContainer.append(Mustache.render(outputUserTemplate, { text, date: getDateTime() }));

        const params = serializeFormToJson("SessionParameters");

        // include clean model name in params
        const selectedModel = document.getElementById("SessionConfig_Model")?.value;


        if (selectedModel) params.Model = normalizeModelName(selectedModel);

        inferenceSession = await connection
            .stream("SendPrompt", text, params)
            .subscribe({
                next: onResponse,
                complete: onResponse,
                error: onError,
            });

        scrollToBottom(true);
    };

    const cancelPrompt = async () => {
        if (inferenceSession) inferenceSession.dispose();
    };

    const loadModel = async () => {
        const sessionParams = serializeFormToJson("SessionParameters");

        // safely attach normalized model
        const selectedModel = document.getElementById("SessionConfig_Model")?.value;

        if (selectedModel) sessionParams.Model = normalizeModelName(selectedModel);

        loaderShow();
        disableControls();
        disablePromptControls();
        $("#load").attr("disabled", "disabled");

        await connection.invoke("LoadModel", sessionParams, sessionParams);
    };

    const unloadModel = async () => {
        await cancelPrompt();
        disableControls();
        enablePromptControls();
        $("#load").removeAttr("disabled");
    };

    const serializeFormToJson = (form) => {
        const formDataJson = {};
        const formData = new FormData(document.getElementById(form));
        formData.forEach((value, key) => {
            if (key.includes(".")) key = key.split(".")[1];
            if (!isNaN(value) && value.trim() !== "") formDataJson[key] = parseFloat(value);
            else if (value === "true" || value === "false") formDataJson[key] = (value === "true");
            else formDataJson[key] = value;
        });
        return formDataJson;
    };

    const enableControls = () => $(".input-control").removeAttr("disabled");
    const disableControls = () => $(".input-control").attr("disabled", "disabled");

    const enablePromptControls = () => {
        $("#load").show();
        $("#unload").hide();
        $(".prompt-control").removeAttr("disabled");
    };
    const disablePromptControls = () => $(".prompt-control").attr("disabled", "disabled");

    const clearOutput = () => outputContainer.empty();
    const getDateTime = () => new Date().toLocaleString();
    const randomString = () => Math.random().toString(36).slice(2);
    const scrollToBottom = (force) => {
        const scrollTop = scrollContainer.scrollTop();
        const scrollHeight = scrollContainer[0].scrollHeight;
        if (force || scrollTop + 70 >= scrollHeight - scrollContainer.innerHeight()) {
            scrollContainer.scrollTop(scrollContainer[0].scrollHeight);
        }
    };
    const loaderShow = () => $(".spinner").show();
    const loaderHide = () => $(".spinner").hide();

    //EVENT BINDINGS
    $("#load").on("click", loadModel);
    $("#unload").on("click", unloadModel);
    $("#send").on("click", sendPrompt);
    $("#clear").on("click", clearOutput);
    $("#cancel").on("click", cancelPrompt);
    chatInput.on("keydown", (event) => {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            sendPrompt();
        }
    });

    //SIGNALR 
    connection.on("OnStatus", onStatus);
    connection.on("OnError", onError);
    connection.on("OnResponse", onResponse);
    connection.start();
};
