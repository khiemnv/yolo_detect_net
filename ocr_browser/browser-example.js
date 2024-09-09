let g_ocr;
let g_onnxOptions = {
  executionProviders: ["wasm"],
  graphOptimizationLevel: "all",
};
let g_canvas = document.getElementById("canvas_id");
let g_debugcanvas = document.getElementById("canvas_debug");
let clipper = ClipperLib;

class Ocr {
  static async create(options = {}) {
    const detection = await Detection.create(options);
    const recognition = await Recognition.create(options);
    return new Ocr({ detection, recognition });
  }

  constructor({ detection, recognition }) {
    this.detection = detection;
    this.recognition = recognition;
  }

  async detect(image, onnxOptions) {
    try {
      const lineImages = await this.detection.run(image, onnxOptions);
      const texts = await this.recognition.run(lineImages, onnxOptions);
      return texts;
    } catch (ex) {
      console.log(ex);
    }
  }
}
async function main() {
  g_ocr = await Ocr.create({
    isDebug: true,
    models: {
      detectionPath: "/assets/ch_PP-OCRv4_det_infer.onnx",
      // recognitionPath: "/assets/ch_PP-OCRv4_rec_infer.onnx",
      recognitionPath: "/assets/japan_PP-OCRv3_rec_infer.onnx",
      dictionaryPath: "/assets/japan_dict.txt",
    },
    onnxOptions: g_onnxOptions,
  });

  // document.querySelector(".hide").style.visibility = "visible";
  document.querySelector("#title").textContent = "OCR is ready";

  createApp(runOcr);
}

async function runOcr({ imageUrl }) {
  const startTime = new Date().valueOf();
  const result = await g_ocr.detect(imageUrl, g_onnxOptions);
  const duration = new Date().valueOf() - startTime;

  return {
    text: result.map((v) => `${v.mean.toFixed(2)} ${v.text}`).join("\n"),
    duration,
  };
}
  const resultTextEl = document.querySelector("#result-text");
  const performanceEl = document.querySelector("#performance");
  const resultImageEl = document.querySelector("#result-image");
  const inputImageEl = document.querySelector("#input-image");
function createApp(runOcr) {
  inputImageEl.addEventListener("change", async (event) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }
    const imageUrl = URL.createObjectURL(file);
    await handleChange(imageUrl);
  });
  const handleChange = async (imageUrl) => {
    // document.querySelectorAll("canvas").forEach((el) => el.remove());
    resultTextEl.textContent = "Working in progress...";
    resultImageEl.setAttribute("src", imageUrl);
    const { text, duration } = await runOcr({ imageUrl });
    resultTextEl.textContent = text;
    performanceEl.textContent = `Performance: ${duration}ms (Close Chrome DevTools to get accureate result)`;
  };
}

main();
document.getElementById("process_btn").onclick = async (event) => {
  event.preventDefault();
  var imageUrl = document.querySelector("#result-image").src;
  const { text, duration } = await runOcr({ imageUrl });
  console.log(text, duration);
  resultTextEl.textContent = text
};
