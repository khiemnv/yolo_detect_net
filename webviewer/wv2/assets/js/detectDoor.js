/* ↓Init↓ */
var flgPlayVideo = false;
var countDevices = 0;
var setting_background = $("input[name=setting_background]:checked").val();
$("#info_FolderSave").text("Text info");
var ngCount = 0;
/* ↑Init↑ */

var img_Result_inLeft = document.getElementById("img_Result_inLeft");
var img_Sample = document.getElementById("img_Sample");
var img_Sample_back = document.getElementById("img_Sample_back");
var img_sample_label = document.getElementById("img_sample_label");
var img_sample_label2 = document.getElementById("img_sample_label2");

const video_inLeft = document.getElementById("video_inLeft");
const video_inRight = document.getElementById("video_inRight");
const video_outLeft = document.getElementById("video_outLeft");
const video_outRight = document.getElementById("video_outRight");

const canvas_inLeft = document.getElementById("canvas_inLeft");
const canvas_inRight = document.getElementById("canvas_inRight");
const canvas_outLeft = document.getElementById("canvas_outLeft");
const canvas_outRight = document.getElementById("canvas_outRight");
const Log = {
    Error: (msg) => {
        console.log("[Error]", msg);
    },
    Debug: (msg) => {
        console.log("[Debug]", msg);
    },
};

var data_Result_inLeft = null;
if (data_Result_inLeft == null) {
    var loc = window.location.pathname;
    var dir = loc.substring(0, loc.lastIndexOf("/"));
    data_Result_inLeft = {
        title: "img_Result_inLeft",
        img: "file:/" + dir + "/img/NoResult.jpg",
    };
}
img_Result_inLeft.src = data_Result_inLeft.img;

var labelCamera = null;
if (labelCamera == null) {
    labelCamera = "16MP USB Camera"; //"Dell Webcam WB5023";//BUFFALO BSW32KM01H Webcam
}

function gotStream() {
    video_inLeft.style.display = "block";
    video_inRight.style.display = "block";
    img_Result_inLeft.style.display = "none";
    $(".led-box").css("display", "block");
    flgPlayVideo = true;
    // navigator.mediaDevices.enumerateDevices().then((devices) => {
    //   countDevices = devices.length;
    // });
}

function Capture_Image() {
    video_inLeft.pause();
    video_inRight.pause();
    capture();
    setTimeout(function () {
        var fname = "Capture_Image";
        var parameter = canvas_inRight.toDataURL();
        var event = new MessageEvent(fname, {
            view: window,
            bubbles: false,
            cancelable: false,
            data: parameter,
        });
        document.dispatchEvent(event);
    }, 200);
}
function Change_Bgr(bgr) {
    if (bgr != "背景8") {
        var dir = loc.substring(0, loc.lastIndexOf("/"));
        var link = "file:/" + dir + "/img/" + bgr;
        var url = "url(" + link + ")";
        var bodyContent = document.getElementById("bodyMain");
        bodyContent.style.backgroundImage = url;
    } else {
        $("body").css(
            "background",
            "linear-gradient(180deg, rgba(40, 158, 137, 0.9) 0%, rgba(40, 158, 137, 0.2) 100%)"
        );
    }
}

$("#settingModal").on("shown.bs.modal", function () {
    setting_background = $("input[name=setting_background]:checked").val();
});
function Cancel_Setting() {
    $("input[name=setting_background]").val([setting_background]);
    Change_Bgr(setting_background);
}
function Apply_Setting() {
    if ($("input[name=setting_background]:checked").val() != setting_background) {
        setting_background = $("input[name=setting_background]:checked").val();
        Change_Bgr(setting_background);
    }
    $("#settingModal").modal("hide");
}

async function startCamera_in(swap = false) {
    try {
        try {
            if (window.stream) {
                window.stream.getTracks().forEach((track) => {
                    track.stop();
                });
            }
        } catch (ex) {
            console.log(ex.message)
        }

        var devices = await navigator.mediaDevices.enumerateDevices();
        console.log(devices);
        sendDataToNet("devices", JSON.stringify(devices));
        var vdevices = devices.filter((x) => x.kind == "videoinput");
        console.log(vdevices);
        var cb = async (device, i) => {
            console.log(device)
            const constraints_in = {
                video: {
                    width: { min: 2500, ideal: 4656 },
                    height: { min: 1800, ideal: 3496 },
                    deviceId: { exact: device.deviceId },
                },
            };
            try {
                var stream = await navigator.mediaDevices.getUserMedia(
                    constraints_in
                );
                const videoTracks = stream.getVideoTracks();
                console.log(`Using video device: ${videoTracks[0].label}`);
                stream.onremovetrack = () => {
                    console.log("Stream ended");
                };
                if (i == 0) {

                    video_inLeft.srcObject = stream;
                } else {
                    video_inRight.srcObject = stream;
                }
                //video.srcObject = stream;
                return stream;
            } catch (ex) {
                console.log(ex.message);
                return null;
            }
        }
        //var streams = await Promise.all(
        for (i = 0; i < vdevices.length; i++) {
            await cb(vdevices[i], i);
        }
        //);
        // console.log(streams);
        //streams = streams.filter(x=>!!x);

        // const trackSettings = streams.map((x) => x.getTracks()[0].getSettings());
        // console.log(trackSettings);
        //if (swap == false) {
        //    gotStream_Leftin(streams[0], 4656, 3496);
        //    gotStream_Rightin(streams[1], 4656, 3496);
        //} else {
        //  gotStream_Leftin(streams[1]);
        //  gotStream_Rightin(streams[0]);
        //}

        sendDataToNet("OnState", "getStream_Success");
    } catch (ex) {
        console.log(ex.message);
        sendDataToNet("OnState", "getStream_Error");
    }
}

function startCamera_out() {
    if (window.stream) {
        window.stream.getTracks().forEach((track) => {
            track.stop();
        });
    }
    navigator.mediaDevices.enumerateDevices().then((devices) => {
        var checkConnect_out = false;
        devices.forEach((device) => {
            if (device.label == labelCamera) {
                const constraints_out = {
                    video: {
                        width: { min: 2500, ideal: 4656 },
                        height: { min: 1800, ideal: 3496 },
                        deviceId: { exact: device.deviceId },
                    },
                };
                if (checkConnect_out == false) {
                    checkConnect_out = true;
                    navigator.mediaDevices
                        .getUserMedia(constraints_out)
                        .then(gotStream_Leftout);
                } else {
                    navigator.mediaDevices
                        .getUserMedia(constraints_out)
                        .then(gotStream_Rightout);
                }
            }
        });
    });
}

function gotStream_Leftin(stream, w, h) {
    window.stream = stream; // make stream available to console
    video_inLeft.srcObject = stream;
    //const trackSettings = stream.getTracks()[0].getSettings();
    //canvas_inLeft.width = trackSettings.width;
    //canvas_inLeft.height = trackSettings.height;
    canvas_inLeft.width = w;
    canvas_inLeft.height = h;
    gotStream();

    // feedback
    sendDataToNet("OnState", "gotStream_Leftin");
}

function gotStream_Leftout(stream) {
    window.stream = stream; // make stream available to console
    video_outLeft.srcObject = stream;
    gotStream();
}

function gotStream_Rightin(stream, w, h) {
    window.stream = stream; // make stream available to console
    video_inRight.srcObject = stream;
    //const trackSettings = stream.getTracks()[0].getSettings();
    //canvas_inRight.width = trackSettings.width;
    //canvas_inRight.height = trackSettings.height;
    canvas_inLeft.width = w;
    canvas_inLeft.height = h;
    // gotStream();

    // feedback
    sendDataToNet("OnState", "gotStream_Rightin");
}

function gotStream_Rightout(stream) {
    window.stream = stream; // make stream available to console
    video_outRight.srcObject = stream;
    gotStream();
}

/**
 * Tool.Set(img,img)
 * @param {left base 64 img} data1
 * @param {right base 64 img} data2
 */
function setImg(data1, data2) {
    canvas_inRight.width = 4656;
    canvas_inRight.height = 3496;
    canvas_inLeft.width = 4656;
    canvas_inLeft.height = 3496;

    var image = new Image();
    image.onload = function () {
        canvas_inLeft
            .getContext("2d")
            .drawImage(image, 0, 0, canvas_inLeft.width, canvas_inLeft.height);
        canvas_inLeft.style.display = "block";
        document.querySelector("#video_inLeft").style.display = "none";
    };
    image.src = data1;

    var image2 = new Image();
    image2.onload = function () {
        canvas_inRight
            .getContext("2d")
            .drawImage(image2, 0, 0, canvas_inRight.width, canvas_inRight.height);
        canvas_inRight.style.display = "block";
        document.querySelector("#video_inRight").style.display = "none";
    };
    image2.src = data2;
}

/**
 * Tool.Send()
 * @param {front|back} doorSide
 */
function showSample(doorSide) {
    if (doorSide === "none") {
        img_Sample.style.display = "none";
        img_Sample_back.style.display = "none";
        img_sample_label.innerText = "";
        img_sample_label2.innerText = "";
    } else if (doorSide === "front") {
        img_Sample.style.display = "block";
        img_Sample_back.style.display = "none";
        img_sample_label.innerText = "オモテ";
        img_sample_label2.innerText = "オモテ";
    } else {
        img_Sample.style.display = "none";
        img_Sample_back.style.display = "block";
        img_sample_label.innerText = "ウラ";
        img_sample_label2.innerText = "ウラ";
    }
}

function capture() {
    //flgPlayVideo = false;
    //canvas_inLeft = document.getElementById("canvas_inLeft");
    var video_Left = document.querySelector("#video_inLeft");
    canvas_inLeft.style.width = "200px";
    canvas_inLeft
        .getContext("2d")
        .drawImage(video_Left, 0, 0, canvas_inLeft.width, canvas_inLeft.height);

    //canvas_inRight = document.getElementById("canvas_inRight");
    var video_Right = document.querySelector("#video_inRight");
    canvas_inRight.style.width = "200px";
    canvas_inRight
        .getContext("2d")
        .drawImage(video_Right, 0, 0, canvas_inRight.width, canvas_inRight.height);
}
var parameter_send_total_two = "";

/**
 * btn_enter: 0 (debug tool), 1|2 (normal), 1 (TAKE_SAMPLE)
 */
function sendImage(btn_enter) {
    Log.Debug("sendImage btn_enter: " + btn_enter);
    if (btn_enter != 0) {
        video_inLeft.pause();
        video_inRight.pause();
        capture();
    }

    var parameter_inRight = canvas_inRight.toDataURL("image/jpeg", 0.9);
    var parameter_inLeft = canvas_inLeft.toDataURL("image/jpeg", 0.9);
    // var parameter_inRight = canvas_inRight.toDataURL().split(';base64,')[1];; // png
    // var parameter_inLeft = canvas_inLeft.toDataURL().split(';base64,')[1];; // png

    var fname = "Capture_Image";
    parameter_send_total_two = parameter_inLeft + "@" + parameter_inRight;
    // setTimeout(function () {
    sendDataToNet(fname, parameter_send_total_two);
    // }, 300);

    if (btn_enter != 0) {
        video_inLeft.play();
        video_inRight.play();
        // 2回目クリックボタンを押した
        if (btn_enter == 2) {
            video_inLeft.pause();
            video_inRight.pause();
        }
    }
}
var btn_enter = 0;
var barcode = "";
var interval;
var start_ReadBarCode = false;
var flg_scanbarcode = false;
var g_isSending = false;
var g_lastEnter = Date.now();
var g_isTakeSample = false;
var g_delayCapture = 0;
var g_pinNG = false;
const STATE_INIT = 0;
const STATE_SCAN_BARCODE = 2;
const STATE_CAPTURE = 3;
var g_state = STATE_INIT;
///////////////////////////////////////////////////////
if (btn_enter == 0) {
    showSample("front");
}
///////////////////////////////////////////////////////
document.addEventListener(
    "keydown",
    (event) => {
        if (login_Status == true) {
            if (flg_scanbarcode == true) {
                if (g_state !== STATE_CAPTURE) { return; }

                if (event.code == "Enter") {
                    const millis = Date.now() - g_lastEnter;
                    if (millis < 1000) {
                        Log.Debug("Double Enter: " + millis);
                        return;
                    }

                    g_lastEnter = Date.now();
                    if (!g_isSending) {
                        g_isSending = true;
                        delayCapture();
                    }
                }
            } else {
                if (g_state !== STATE_SCAN_BARCODE) { return; }

                if (event.code == "End") {
                    if (barcode) {
                        handleBarcode(barcode);
                    }
                    start_ReadBarCode = false;
                    return;
                }
                if (start_ReadBarCode) {
                    barcode += event.key;
                }
                if (event.key == "Escape" && start_ReadBarCode == false) {
                    start_ReadBarCode = true;
                }
            }
        }
    },
    false
);
function handleCapture() {
    // if (g_isTakeSample) {
    //   Log.Debug("takeSample");
    //   sendImage(1);
    //   return;
    // }

    Log.Debug("handleCapture");
    btn_enter++;
    if (btn_enter == 1) {
        showSample("back");
    } else if (btn_enter == 2) {
        showSample("front");
    } else {
        return;
    }
    sendImage(btn_enter);
}
var barcode = "";
function handleBarcode(scanned_barcode) {
    barcode = scanned_barcode.trim();
    barcode = barcode.split("Shift").join("");
    if (barcode != "") {
        // var imgName = (btn_enter == 0) ? "DoorFront.png" : "DoorBack.png";
        // img_Sample.src = "img/" + "model_" + barcode + "/" + imgName;
        showSample("none");
    }
    document.querySelector("#viewbarcodeId").innerHTML = barcode;
    flg_scanbarcode = true;
    $("#viewImgScanBarcodeId").css("display", "none");
    //img_Sample.src = "img/" + scanned_barcode + ".png";
    //send barcode to net
    var fname = "BarCode";
    sendDataToNet(fname, barcode);
}
function receiveResultBarCode(result, frontBase64, backBase64) {
    if (result.trim() == "NG") {
        $("#bodyContentId").css("display", "block");
        $("#viewImgScanBarcodeId").css("display", "block");
        barcode = "";
        document.querySelector("#viewbarcodeId").innerHTML = "";
        flg_scanbarcode = false;
    } else {
        frontBase64 = frontBase64 || "img/DoorFront.png";
        backBase64 = backBase64 || "img/DoorBack.png";
        img_Sample.src = frontBase64;
        img_Sample_back.src = backBase64;
        showSample("front");
        g_state = STATE_CAPTURE;
    }
}

function execCmd(cmd, params) {
    switch (cmd) {
        case "spin_show": // {showHide:1/0, message:}
            var { showHide, message } = params;
            var waitScreen = document.getElementById("waitId");
            waitScreen.style.display = showHide == 0 ? "none" : "block";
            if (showHide == 1 && message) {
                document.getElementById("waitMsgId").innerText = message;
            } else {
                document.getElementById("waitMsgId").innerText = "";
            }

            // feedback
            sendDataToNet("OnState", showHide == 0 ? "spin_hidden" : "spin_showed");
            break;
        case "disconnected":
            var img = document.getElementById("disconnected_img");
            img.style.display = params == 0 ? "none" : "block";

            // feedback
            sendDataToNet(
                "OnState",
                params == 0 ? "disconnected_hidden" : "disconnected_showed"
            );
            break;
        case "img_received":
            g_isSending = false;
            Log.Debug("[img_received] received count:" + params);
            break;
        case "draw_rect":
            var [x, y, w, h, x2, y2, w2, h2] = params;
            {
                var canvas = document.getElementById("rect_inLeft");
                canvas.style.display = "block";
                var ctx = canvas.getContext("2d");
                ctx.clearRect(0, 0, canvas.width, canvas.height);
                ctx.lineWidth = "20";
                ctx.strokeStyle = "red";
                ctx.strokeRect(x, y, w, h);
            }
            {
                var canvas = document.getElementById("rect_inRight");
                canvas.style.display = "block";
                var ctx = canvas.getContext("2d");
                ctx.clearRect(0, 0, canvas.width, canvas.height);
                ctx.lineWidth = "20";
                ctx.strokeStyle = "red";
                ctx.strokeRect(x2, y2, w2, h2);
            }
            break;
        case "change_mode":
            if (params === "TAKE_SAMPLE") {
                g_isTakeSample = true;
            } else {
                g_isTakeSample = false;
            }
            var img = document.getElementById("take_sample_img");
            img.style.display = g_isTakeSample ? "block" : "none";
            break;
        case "delay_capture":
            g_delayCapture = parseInt(params);
            document.getElementById("delay_img").style.display = g_delayCapture
                ? "block"
                : "none";
            break;
        // case "set_camera_label":
        //   labelCamera = params; // "16MP USB Camera"
        //   startCamera_in();
        //   break;
        case "re_startCamera_in":
            setTimeout(startCamera_in, params * 1000);
            break;
    }
}

function pinNG(b) {
    g_pinNG = b;
    hideNG();
}
function hideNG() {
    resetNG();
    ngCount = 0;
}
function resetNG() {
    var result_history = document.getElementById("result_history");
    result_history.src = "";
    var ng_count = document.getElementById("ng_count");
    ng_count.innerText = "";
}
function setNG() {
    var result_history = document.getElementById("result_history");
    if (ngCount > 0) {
        result_history.src = "img/ResultNG.png";
        var ng_count = document.getElementById("ng_count");
        ng_count.innerText = ngCount;
    }
}

function receiveResultFromNet(result) {
    var screen_Circle = document.getElementById("resultId");
    var circle_Ele = document.getElementById("resultCircleId");
    var result_img = document.getElementById("img_resultId");

    // g_pinNG && resetNG();
    if (result.trim() == "OK") {
        screen_Circle.style.display = "block";
        result_img.src = "img/ResultOK.png";
        // screen_Circle.style.backgroundColor = "green";
        // circle_Ele.innerHTML = "OK";
        // show NG, OK 3s
        setTimeout(function () {
            screen_Circle.style.display = "none";
            // g_pinNG && setNG();
        }, 2000);
    } else if (result.trim() == "NG") {
        ngCount++;
        screen_Circle.style.display = "block";
        // screen_Circle.style.backgroundColor = "red";
        // circle_Ele.innerHTML = "NG";
        result_img.src = "img/ResultNG.png";
        setTimeout(function () {
            screen_Circle.style.display = "none";
            g_pinNG && setNG();
        }, 2000);
    }

    $("#bodyContentId").css("display", "block");
    $("#viewImgScanBarcodeId").css("display", "block");
    video_inLeft.play();
    video_inRight.play();
    btn_enter = 0;
    flg_scanbarcode = false;
    // img_Sample.src = "img/" + "model_" + barcode + "/" + "DoorFront.png";
    showSample("front");
    barcode = "";
    document.querySelector("#viewbarcodeId").innerHTML = "";
    g_state = STATE_SCAN_BARCODE;
}
var login_Status = false;
document.getElementById("btnCheck").addEventListener("click", function () {
    const username = document.getElementById("username").value.trim();
    const password = document.getElementById("password").value.trim();
    const autologin = document.getElementById("autologin").checked;
    if (username != "" && password != "") {
        var fname = "CheckLogin";
        var user = username + "@" + password + "@" + autologin;
        sendDataToNet(fname, user);
    } else {
        alert("ユーザーとパスワードを入力してください。");
    }
});
function receiveLoginFromNet(isLogin) {
    if (isLogin == "OK") {
        //alert('Login Sucess');
        $("#bodyContentId").css("display", "block");
        $("#viewImgScanBarcodeId").css("display", "block");
        $("#loginFormId").css("display", "none");
        login_Status = true;
        g_state = STATE_SCAN_BARCODE;
    } else {
        alert("Username or Password is wrong");
        login_Status = false;
    }
}
function sendDataToNet(funcName, data) {
    var event = new MessageEvent(funcName, {
        view: window,
        bubbles: false,
        cancelable: false,
        data: data,
    });
    //document.dispatchEvent(event);
    window.chrome.webview.postMessage(JSON.stringify({ funcName, data }));
}

document.getElementById("btnLogout").addEventListener("click", function () {
    var event = new MessageEvent("LogoutRequest", {
        view: window,
        bubbles: false,
        cancelable: false,
        data: "",
    });
    document.dispatchEvent(event);
});

function LogoutResponse() {
    $("#bodyContentId").css("display", "none");
    $("#viewImgScanBarcodeId").css("display", "none");
    $("#loginFormId").css("display", "flex");
    login_Status = false;
}

function AutoLogin() {
    if (login_Status == true) {
        receiveLoginFromNet("OK");
    }
}

/** */
document.getElementById("bodyMain").ondragover = function (ev) {
    Log.Debug("File(s) in drop zone");
    ev.preventDefault();
};
document.getElementById("bodyMain").ondrop = async function (ev) {
    ev.preventDefault();
    var imgfile = [];
    if (ev.dataTransfer.items) {
        // Use DataTransferItemList interface to access the file(s)
        [...ev.dataTransfer.items].forEach((item, i) => {
            // If dropped items aren't files, reject them
            if (item.kind === "file") {
                var file = item.getAsFile();
                Log.Debug(`… file[${i}].name = ${file.name}`);
                imgfile.push(file);
            }
        });
    } else {
        // Use DataTransfer interface to access the file(s)
        [...ev.dataTransfer.files].forEach((file, i) => {
            Log.Debug(`… file[${i}].name = ${file.name}`);
            imgfile.push(file);
        });
    }

    if (imgfile.length != 2) {
        alert("Please drop 2 imgs!");
        return;
    }

    var bmp = await createImageBitmap(imgfile[0]);
    canvas_inLeft
        .getContext("2d")
        .drawImage(bmp, 0, 0, canvas_inLeft.width, canvas_inLeft.height);
    canvas_inLeft.style.display = "block";
    video_inLeft.style.display = "none";

    bmp = await createImageBitmap(imgfile[1]);
    canvas_inRight
        .getContext("2d")
        .drawImage(bmp, 0, 0, canvas_inLeft.width, canvas_inLeft.height);
    canvas_inRight.style.display = "block";
    video_inRight.style.display = "none";

    sendImage(0);
};

const readImgFile = (inputFile) => {
    const temporaryFileReader = new FileReader();

    return new Promise((resolve, reject) => {
        temporaryFileReader.onerror = () => {
            temporaryFileReader.abort();
            reject(new DOMException("Problem parsing input file."));
        };

        temporaryFileReader.onload = () => {
            resolve(temporaryFileReader.result);
        };
        temporaryFileReader.readAsDataURL(inputFile);
    });
};

// delay capture
function delayCapture() {
    if (g_delayCapture) {
        // show count down
        showCountDown(g_delayCapture);

        var count = 0;
        var nIntervId = setInterval(() => {
            count++;
            if (count >= g_delayCapture) {
                clearInterval(nIntervId);
                nIntervId = null;

                // hide count down
                hideCountDown();

                // capture
                handleCapture();
            } else {
                // show count down
                showCountDown(g_delayCapture - count);
            }
        }, 1000);
    } else {
        handleCapture();
    }
}
function showCountDown(n) {
    $(".count_down_container").show();
    $(".count_down").text(n);
}
function hideCountDown() {
    $(".count_down_container").hide();
    $(".count_down").text("");
}
/*****************************test camera**********************************
var t = 0;
function test(){
  //
  t++;
  console.log("" + t);
  sendImage(t);
  if(t == 2){
    t = 0;
    $("#bodyContentId").css('display', 'block');
    $("#viewImgScanBarcodeId").css('display', 'none');
    video_inLeft.play();
    video_inRight.play();
  }
  setTimeout(test,10000);
}
test();
********************************************************************** */
AutoLogin();

// notify load script complete
sendDataToNet("OnState", "script_loaded");

// console.log("debug");
//receiveLoginFromNet("OK");
//handleBarcode("7230B086XA");
// receiveResultBarCode("OK");
// handleBarcode("7230B086XA");
// execCmd("change_mode", "TAKE_SAMPLE");
// execCmd("disconnected", 1);
// execCmd("delay_capture", 5);
// hideNG();
//startCamera_in(false);

window.chrome.webview.postMessage(JSON.stringify({ url: window.document.URL }));