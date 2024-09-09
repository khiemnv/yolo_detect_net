class Recognition extends ModelBase {
  static async create({
    models,
    onnxOptions = {},
    ...restOptions
  }) {
    const recognitionPath = models.recognitionPath;
    const dictionaryPath = models.dictionaryPath;
    const model = await InferenceSession.create(recognitionPath, onnxOptions);
    const dictionaryText = await FileUtils.read(dictionaryPath);
    const dictionary = [...dictionaryText.split('\n'), ' '];
    return new Recognition({
      model,
      options: restOptions
    }, dictionary);
  }
  constructor(options, dictionary) {
    super(options);
    this._dictionary = dictionary;
  }
  async run(lineImages, {
    onnxOptions = {}
  } = {}) {
    const modelDatas = await Promise.all(
    // Detect text from each line image
    lineImages.map(async (lineImage, index) => {
      // Resize Image to 48px height
      //  - height must <= 48
      //  - height: 48 is more accurate then 40, but same as 30
      const image = await lineImage.image.resize({
        height: 48
      });
      // this.debugImage(lineImage.image, `./output/out9-line-${index}.jpg`)
      // this.debugImage(image, `./output/out9-line-${index}-resized.jpg`)

      // transform image data to model data
      const modelData = this.imageToInput(image, {
        // mean: [0.5, 0.5, 0.5],
        // std: [0.5, 0.5, 0.5],
      });
      return modelData;
    }));
    const allLines = [];
    // console.time('Recognition')
    for (const modelData of modelDatas) {
      // Run model for each line image
      const output = await this.runModel({
        modelData,
        onnxOptions
      });
      // use Dictoinary to decode output to text
      const lines = await this.decodeText(output);
      allLines.unshift(...lines);
    }
    // console.timeEnd('Recognition')
    const result = calculateBox({
      lines: allLines,
      lineImages
    });
    return result;
  }
  decodeText(output) {
    const data = output;
    const predLen = data.dims[2];
    const line = [];
    let ml = data.dims[0] - 1;
    for (let l = 0; l < data.data.length; l += predLen * data.dims[1]) {
      const predsIdx = [];
      const predsProb = [];
      for (let i = l; i < l + predLen * data.dims[1]; i += predLen) {
        const tmpArr = data.data.slice(i, i + predLen);
        const tmpMax = tmpArr.reduce((a, b) => Math.max(a, b), Number.NEGATIVE_INFINITY);
        const tmpIdx = tmpArr.indexOf(tmpMax);
        predsProb.push(tmpMax);
        predsIdx.push(tmpIdx);
      }
      line[ml] = decode(this._dictionary, predsIdx, predsProb, true);
      ml--;
    }
    return line;
  }
}
function decode(dictionary, textIndex, textProb, isRemoveDuplicate) {
  const ignoredTokens = [0];
  const charList = [];
  const confList = [];
  for (let idx = 0; idx < textIndex.length; idx++) {
    if (textIndex[idx] in ignoredTokens) {
      continue;
    }
    if (isRemoveDuplicate) {
      if (idx > 0 && textIndex[idx - 1] === textIndex[idx]) {
        continue;
      }
    }
    charList.push(dictionary[textIndex[idx] - 1]);
    if (textProb) {
      confList.push(textProb[idx]);
    } else {
      confList.push(1);
    }
  }
  let text = '';
  let mean = 0;
  if (charList.length) {
    text = charList.join('');
    let sum = 0;
    confList.forEach(item => {
      sum += item;
    });
    mean = sum / confList.length;
  }
  return {
    text,
    mean
  };
}
function calculateBox({
  lines,
  lineImages
}) {
  let mainLine = lines;
  const box = lineImages;
  for (const i in mainLine) {
    const b = box[mainLine.length - Number(i) - 1].box;
    for (const p of b) {
      p[0] = p[0];
      p[1] = p[1];
    }
    mainLine[i]['box'] = b;
  }
  mainLine = mainLine.filter(x => x.mean >= 0.5);
  mainLine = afAfRec(mainLine);
  return mainLine;
}
function afAfRec(l) {
  const line = [];
  const ind = new Map();
  for (const i in l) {
    let item = l[i].box;
    ind.set(item, Number(i));
  }
  function calculateAverageHeight(boxes) {
    let totalHeight = 0;
    for (const box of boxes) {
      const [[, y1],, [, y2]] = box;
      const height = y2 - y1;
      totalHeight += height;
    }
    return totalHeight / boxes.length;
  }
  function groupBoxesByMidlineDifference(boxes) {
    const averageHeight = calculateAverageHeight(boxes);
    const result = [];
    for (const box of boxes) {
      const [[, y1],, [, y2]] = box;
      const midline = (y1 + y2) / 2;
      const group = result.find(b => {
        const [[, groupY1],, [, groupY2]] = b[0];
        const groupMidline = (groupY1 + groupY2) / 2;
        return Math.abs(groupMidline - midline) < averageHeight / 2;
      });
      if (group) {
        group.push(box);
      } else {
        result.push([box]);
      }
    }
    for (const group of result) {
      group.sort((a, b) => {
        const [ltA] = a;
        const [ltB] = b;
        return ltA[0] - ltB[0];
      });
    }
    result.sort((a, b) => a[0][0][1] - b[0][0][1]);
    return result;
  }
  const boxes = groupBoxesByMidlineDifference([...ind.keys()]);
  for (const i of boxes) {
    const t = [];
    let m = 0;
    for (const j of i) {
      if (!ind.get(j)) continue;
      const x = l[ind.get(j)];
      t.push(x.text);
      m += x.mean;
    }
    let box = undefined;
    if (i.at(0) && i.at(-1)) {
      box = [i.at(0)[0], i.at(-1)[1], i.at(-1)[2], i.at(0)[3]];
    }
    line.push({
      mean: m / i.length,
      text: t.join(' '),
      box: box
    });
  }
  return line;
}