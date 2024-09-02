console.log("index.js");
document.getElementById("detect_btn").onclick = ()=>detect("circle_img");
document.getElementById("detect_btn2").onclick = ()=>detect("eclipse_img");
async function detect(imdid) {
  let img = document.getElementById(imdid);
  let resizedCtx = document.getElementById("resized_ctx").getContext("2d");
  resizedCtx.drawImage(img, 0, 0, 240, 240);
  let modelResolution = [256, 256];
  const imageData = resizedCtx.getImageData(
    0,
    0,
    modelResolution[0],
    modelResolution[1]
  );

  // NOTE: inputTensor [1,3,256,256]
  const inputTensor = await ort.Tensor.fromImage(
    imageData,
    (options = { resizedWidth: 256, resizedHeight: 256 })
  );

  // model input: images, float32[1,3,256,256]
  //       output: output0, float32[1,2]
  let session = await createModelCpu("./best.onnx");

  let [outputTensor, inferenceTime] = await runModelUtils.runModel(
    session,
    inputTensor
  );
  console.log(outputTensor);
  document.getElementById("output_txt").textContent = JSON.stringify(outputTensor, "", 4) + "\ninference time: " + inferenceTime + "(ms)"
}

async function createModelCpu(url) {
  const InferenceSession = ort.InferenceSession;
  return await InferenceSession.create(url, {
    executionProviders: ["wasm"],
    graphOptimizationLevel: "all",
  });
}
let runModelUtils = {
  async runModel(model, preprocessedData) {
    const feeds = {};
    feeds[model.inputNames[0]] = preprocessedData;
    const start = Date.now();
    const outputData = await model.run(feeds);
    const end = Date.now();
    const inferenceTime = end - start;
    const output = outputData[model.outputNames[0]];
    return [output, inferenceTime];
  },
};
/**
 * Preprocess raw image data to match Resnet50 requirement.
 */
function preprocess(data, width, height) {
  const dataFromImage = ndarray(new Float32Array(data), [width, height, 4]);
  const dataProcessed = ndarray(new Float32Array(width * height * 3), [
    1,
    3,
    height,
    width,
  ]);

  // Normalize 0-255 to (-1)-1
  ndarray.ops.divseq(dataFromImage, 128.0);
  ndarray.ops.subseq(dataFromImage, 1.0);

  // Realign imageData from [224*224*4] to the correct dimension [1*3*224*224].
  ndarray.ops.assign(
    dataProcessed.pick(0, 0, null, null),
    dataFromImage.pick(null, null, 2)
  );
  ndarray.ops.assign(
    dataProcessed.pick(0, 1, null, null),
    dataFromImage.pick(null, null, 1)
  );
  ndarray.ops.assign(
    dataProcessed.pick(0, 2, null, null),
    dataFromImage.pick(null, null, 0)
  );

  return dataProcessed.data;
}
