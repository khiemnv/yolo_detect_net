const BASE_SIZE = 32;
const InferenceSession = ort.InferenceSession;
class Detection extends ModelBase {
  static async create({ models, onnxOptions = {}, ...restOptions }) {
    const detectionPath = models.detectionPath;
    const model = await InferenceSession.create(detectionPath, onnxOptions);
    return new Detection({
      model,
      options: restOptions,
    });
  }
  async run3(imageUrl, onnxOptions) {
    try {

        const image = await imageFromUrl(imageUrl);
        var canvas = g_canvas;
        var ctx = canvas.getContext("2d");
        ctx.drawImage(image, 0, 0, image.naturalWidth, image.naturalHeight);
        const imageData = ctx.getImageData(
          0,
          0,
          image.naturalWidth,
          image.naturalHeight
        );
        var resizedWidth = Math.ceil(image.naturalWidth/BASE_SIZE) * BASE_SIZE
        var resizedHeight = Math.ceil(image.naturalHeight/BASE_SIZE) * BASE_SIZE
        const inputTensor = await ort.Tensor.fromImage(
          imageData,
          { resizedWidth, resizedHeight}
        );
        const feeds = {};
        feeds[this.model.inputNames[0]] = inputTensor;
        const outputData = await this.model.run(feeds);    
        const modelOutput = outputData[ this.model.outputNames[0]];
        // return output;
        const outputImage = outputToImage(modelOutput, 0.03); // RGBA
        // debugImg(outputImage);
        const image2 = await ImageRaw.open(imageUrl); // {data,width, height}
        const inputImage = await image2.resize(multipleOfBaseSize(image));
        const lineImages = await splitIntoLineImages(outputImage, inputImage);
        // debugImage(lineImages[0].image);
        return lineImages;
    } catch(ex) {
        console.log(ex);
    }
  }
  async run(path, onnxOptions) {
    const image = await ImageRaw.open(path);

    // Resize image to multiple of 32
    //   - image width and height must be a multiple of 32
    //   - bigger image -> more accurate result, but takes longer time
    // inputImage = await Image.resize(image, multipleOfBaseSize(image, { maxSize: 960 }))
    const inputImage = await image.resize(multipleOfBaseSize(image));
    // this.debugImage(inputImage, 'out1-multiple-of-base-size.jpg')

    // Covert image data to model data
    //   - Using `(RGB / 255 - mean) / std` formula
    //   - omit reshapeOptions (mean/std) is more accurate, can creaet a run option for them
    const modelData = this.imageToInput(inputImage, {
      // mean: [0.485, 0.456, 0.406],
      // std: [0.229, 0.224, 0.225],
    });

    // Run the model
    // console.time('Detection')
    const modelOutput = await this.runModel({
      modelData,
      onnxOptions,
    });
    // console.timeEnd('Detection')

    // Convert output data back to image data
    //   - output value is from 0 to 1, a probability, if value > 0.3, it is a text
    //   - returns a black and white image
    const outputImage = outputToImage(modelOutput, 0.03);
    // debugImage(outputImage)

    // Find text boxes, split image into lines
    //   - findContours from the image
    //   - returns text boxes and line images
    const lineImages = await splitIntoLineImages(outputImage, inputImage);
    // debugImage(inputImage)
    // this.debugBoxImage(inputImage, lineImages, "boxes.jpg");
    debugBoxImage(inputImage, lineImages);
    return lineImages;
  }
}
function debugBoxImage(imageData, boxes) {
  // console.log(outputImage);
 
  // g_debugcanvas.getContext("2d").putImageData(imageData2, 0, 0);
  var canvas = g_debugcanvas;
  const ctx = canvas.getContext('2d');
  canvas.width = imageData.width
  canvas.height = imageData.height
  if(false) {
    var { data, width, height } = imageData._imageData;
    const imageData2 = new ImageData(data, width, height);
    ctx.putImageData(imageData2, 0, 0);
  }
  boxes.forEach(({box, image})=>{
    ctx.moveTo(box[0][0], box[0][1]);
    ctx.lineTo(box[1][0], box[1][1]);
    ctx.lineTo(box[2][0], box[2][1]);
    ctx.lineTo(box[3][0], box[3][1]);
    ctx.lineTo(box[0][0], box[0][1]);
    ctx.stroke();

    var { data, width, height} = image;
    const imageData2 = new ImageData(data, width, height);
    ctx.putImageData(imageData2, box[0][0], box[0][1]);
  });

}
function debugImage(imageData) {
  // console.log(outputImage);
  var { data, width, height } = imageData._imageData;
  const imageData2 = new ImageData(data, width, height);
  // g_debugcanvas.getContext("2d").putImageData(imageData2, 0, 0);
  var canvas = g_debugcanvas;
  const ctx = canvas.getContext('2d');
  canvas.width = imageData.width
  canvas.height = imageData.height
  ctx.putImageData(imageData2, 0, 0);
}

function multipleOfBaseSize(image, {
  maxSize
} = {}) {
  let width = image.width;
  let height = image.height;
  if (maxSize && Math.max(width, height) > maxSize) {
    const ratio = width > height ? maxSize / width : maxSize / height;
    width = width * ratio;
    height = height * ratio;
  }
  const newWidth = Math.max(
  // Math.round
  // Math.ceil
  Math.ceil(width / BASE_SIZE) * BASE_SIZE, BASE_SIZE);
  const newHeight = Math.max(Math.ceil(height / BASE_SIZE) * BASE_SIZE, BASE_SIZE);
  return {
    width: newWidth,
    height: newHeight
  };
}
function outputToImage(output, threshold) {
  const height = output.dims[2];
  const width = output.dims[3];
  const data = new Uint8Array(width * height * 4);
  for (const [outIndex, outValue] of output.data.entries()) {
    const n = outIndex * 4;
    const value = outValue > threshold ? 255 : 0;
    data[n] = value; // R
    data[n + 1] = value; // G
    data[n + 2] = value; // B
    data[n + 3] = 255; // A
  }
  return new ImageRaw({
    data,
    width,
    height
  });
}

function int(num) {
  return num > 0 ? Math.floor(num) : Math.ceil(num)
}
async function splitIntoLineImages(image, sourceImage) {
  console.log("splitIntoLineImages")
    const w = image.width;
    const h = image.height;
    const srcData = sourceImage;
    const edgeRect = [];
    var src;
    try {
      src = cvImread(image)
      // var { data, width, height } = image._imageData;
      // const imageData2 = new ImageData(data, width, height);
      // g_canvas.getContext("2d").putImageData(imageData2, 0, 0);
      // src = cv.imread(g_canvas);      
    } catch (error) {
      console.log(error.message)
    }
    cv.cvtColor(src, src, cv.COLOR_RGBA2GRAY, 0);
    const contours = new cv.MatVector();
    const hierarchy = new cv.Mat();
    cv.findContours(src, contours, hierarchy, cv.RETR_LIST, cv.CHAIN_APPROX_SIMPLE);
    for (let i = 0; i < contours.size(); i++) {
      const minSize = 3;
      const cnt = contours.get(i);
      const {
        points,
        sside
      } = getMiniBoxes(cnt);
      if (sside < minSize) continue;
      // TODO sort fast
  
      const clipBox = unclip(points);
      const boxMap = cv.matFromArray(clipBox.length / 2, 1, cv.CV_32SC2, clipBox);
      const resultObj = getMiniBoxes(boxMap);
      const box = resultObj.points;
      if (resultObj.sside < minSize + 2) {
        continue;
      }
      function clip(n, min, max) {
        return Math.max(min, Math.min(n, max));
      }
      const rx = srcData.width / w;
      const ry = srcData.height / h;
      for (let i = 0; i < box.length; i++) {
        box[i][0] *= rx;
        box[i][1] *= ry;
      }
      const box1 = orderPointsClockwise(box);
      box1.forEach(item => {
        item[0] = clip(Math.round(item[0]), 0, srcData.width);
        item[1] = clip(Math.round(item[1]), 0, srcData.height);
      });
      const rect_width = int(linalgNorm(box1[0], box1[1]));
      const rect_height = int(linalgNorm(box1[0], box1[3]));
      if (rect_width <= 3 || rect_height <= 3) continue;
      const c = getRotateCropImage(srcData, box);
      edgeRect.push({
        box,
        image: c
      });
    }
    src.delete();
    contours.delete();
    hierarchy.delete();
    return edgeRect;
  }

  function getMiniBoxes(contour) {
    const boundingBox = cv.minAreaRect(contour);
    const points = Array.from(boxPoints(boundingBox.center, boundingBox.size, boundingBox.angle)).sort((a, b) => a[0] - b[0]);
    let index_1 = 0,
      index_2 = 1,
      index_3 = 2,
      index_4 = 3;
    if (points[1][1] > points[0][1]) {
      index_1 = 0;
      index_4 = 1;
    } else {
      index_1 = 1;
      index_4 = 0;
    }
    if (points[3][1] > points[2][1]) {
      index_2 = 2;
      index_3 = 3;
    } else {
      index_2 = 3;
      index_3 = 2;
    }
    const box = [points[index_1], points[index_2], points[index_3], points[index_4]];
    const side = Math.min(boundingBox.size.height, boundingBox.size.width);
    return {
      points: box,
      sside: side
    };
  }

  
function boxPoints(center, size, angle) {
  const width = size.width
  const height = size.height

  const theta = (angle * Math.PI) / 180.0
  const cosTheta = Math.cos(theta)
  const sinTheta = Math.sin(theta)

  const cx = center.x
  const cy = center.y

  const dx = width * 0.5
  const dy = height * 0.5

  const rotatedPoints = []

  // Top-Left
  const x1 = cx - dx * cosTheta + dy * sinTheta
  const y1 = cy - dx * sinTheta - dy * cosTheta
  rotatedPoints.push([x1, y1])

  // Top-Right
  const x2 = cx + dx * cosTheta + dy * sinTheta
  const y2 = cy + dx * sinTheta - dy * cosTheta
  rotatedPoints.push([x2, y2])

  // Bottom-Right
  const x3 = cx + dx * cosTheta - dy * sinTheta
  const y3 = cy + dx * sinTheta + dy * cosTheta
  rotatedPoints.push([x3, y3])

  // Bottom-Left
  const x4 = cx - dx * cosTheta - dy * sinTheta
  const y4 = cy - dx * sinTheta + dy * cosTheta
  rotatedPoints.push([x4, y4])

  return rotatedPoints
}

function unclip(box) {
  const unclip_ratio = 1.5;
  const area = Math.abs(polygonPolygonArea(box));
  const length = polygonPolygonLength(box);
  const distance = area * unclip_ratio / length;
  const tmpArr = [];
  box.forEach(item => {
    const obj = {
      X: 0,
      Y: 0
    };
    obj.X = item[0];
    obj.Y = item[1];
    tmpArr.push(obj);
  });
  const offset = new clipper.ClipperOffset();
  offset.AddPath(tmpArr, clipper.JoinType.jtRound, clipper.EndType.etClosedPolygon);
  const expanded = [];
  offset.Execute(expanded, distance);
  let expandedArr = [];
  expanded[0] && expanded[0].forEach(item => {
    expandedArr.push([item.X, item.Y]);
  });
  expandedArr = [].concat(...expandedArr);
  return expandedArr;
}

function polygonPolygonArea(polygon) {
  let i = -1,
    n = polygon.length,
    a,
    b = polygon[n - 1],
    area = 0;
  while (++i < n) {
    a = b;
    b = polygon[i];
    area += a[1] * b[0] - a[0] * b[1];
  }
  return area / 2;
}

function polygonPolygonLength(polygon) {
  let i = -1,
    n = polygon.length,
    b = polygon[n - 1],
    xa,
    ya,
    xb = b[0],
    yb = b[1],
    perimeter = 0;
  while (++i < n) {
    xa = xb;
    ya = yb;
    b = polygon[i];
    xb = b[0];
    yb = b[1];
    xa -= xb;
    ya -= yb;
    perimeter += Math.hypot(xa, ya);
  }
  return perimeter;
}

function orderPointsClockwise(pts) {
  const rect = [[0, 0], [0, 0], [0, 0], [0, 0]];
  const s = pts.map(pt => pt[0] + pt[1]);
  rect[0] = pts[s.indexOf(Math.min(...s))];
  rect[2] = pts[s.indexOf(Math.max(...s))];
  const tmp = pts.filter(pt => pt !== rect[0] && pt !== rect[2]);
  const diff = tmp[1].map((e, i) => e - tmp[0][i]);
  rect[1] = tmp[diff.indexOf(Math.min(...diff))];
  rect[3] = tmp[diff.indexOf(Math.max(...diff))];
  return rect;
}
function linalgNorm(p0, p1) {
  return Math.sqrt(Math.pow(p0[0] - p1[0], 2) + Math.pow(p0[1] - p1[1], 2));
}

function cvImread(image) {
  return cv.matFromImageData(image)
}
function getRotateCropImage(imageRaw, points) {
  const img_crop_width = int(Math.max(linalgNorm(points[0], points[1]), linalgNorm(points[2], points[3])));
  const img_crop_height = int(Math.max(linalgNorm(points[0], points[3]), linalgNorm(points[1], points[2])));
  const pts_std = [[0, 0], [img_crop_width, 0], [img_crop_width, img_crop_height], [0, img_crop_height]];
  const srcTri = cv.matFromArray(4, 1, cv.CV_32FC2, flatten(points));
  const dstTri = cv.matFromArray(4, 1, cv.CV_32FC2, flatten(pts_std));

  // 获取到目标矩阵
  const M = cv.getPerspectiveTransform(srcTri, dstTri);
  const src = cvImread(imageRaw);
  const dst = new cv.Mat();
  const dsize = new cv.Size(img_crop_width, img_crop_height);
  // 透视转换
  cv.warpPerspective(src, dst, M, dsize, cv.INTER_CUBIC, cv.BORDER_REPLICATE, new cv.Scalar());
  const dst_img_height = dst.matSize[0];
  const dst_img_width = dst.matSize[1];
  let dst_rot;
  // 图像旋转
  if (dst_img_height / dst_img_width >= 1.5) {
    dst_rot = new cv.Mat();
    const dsize_rot = new cv.Size(dst.rows, dst.cols);
    const center = new cv.Point(dst.cols / 2, dst.cols / 2);
    const M = cv.getRotationMatrix2D(center, 90, 1);
    cv.warpAffine(dst, dst_rot, M, dsize_rot, cv.INTER_CUBIC, cv.BORDER_REPLICATE, new cv.Scalar());
  }
  src.delete();
  srcTri.delete();
  dstTri.delete();
  if (dst_rot) {
    dst.delete();
  }
  return cvImshow(dst_rot || dst);
}

function flatten(arr) {
  return arr.toString().split(',').map(item => +item);
}
function cvImshow(mat) {
  return new ImageRaw({
    data: mat.data,
    width: mat.cols,
    height: mat.rows
  });
}