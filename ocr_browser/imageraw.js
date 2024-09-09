class ImageRaw {
  data;
  _imageData;
  _canvas;

  static async open(url) {
    const image = await imageFromUrl(url);
    const canvas = document.createElement("canvas");
    canvasDrawImage(canvas, image, image.naturalWidth, image.naturalHeight);
    const imageData = canvasGetImageData(canvas);
    return new ImageRaw({
      data: imageData.data,
      width: imageData.width,
      height: imageData.height,
    });
  }

  constructor({ data, width, height }) {
    const newData = Uint8ClampedArray.from(data);
    const canvas = document.createElement("canvas");
    const imageData = new ImageData(newData, width, height);
    canvasPutImageData(canvas, imageData);
    this._canvas = canvas;
    this._imageData = imageData;
    this.data = newData; // this.data is undefined without this line
    this.width = width;
    this.height = height;
  }

  async resize({ width, height }) {
    const newWidth = width || Math.round((this.width / this.height) * height);
    const newHeight = height || Math.round((this.height / this.width) * width);
    const newCanvas = document.createElement("canvas");
    canvasDrawImage(newCanvas, this._canvas, newWidth, newHeight);
    const newImageData = canvasGetImageData(newCanvas);
    return this._apply(newImageData);
  }
  _apply(imageData) {
    canvasPutImageData(this._canvas, imageData)
    this._imageData = imageData
    this.data = imageData.data
    this.width = imageData.width
    this.height = imageData.height
    return this
  }
}

  function canvasDrawImage(canvas, image, width, height) {
    canvas.width = width || image.width
    canvas.height = height || image.height
    const ctx = canvas.getContext('2d')
    ctx.drawImage(image, 0, 0, canvas.width, canvas.height)
  }
  function canvasPutImageData(canvas, imageData, width, height) {
    const ctx = canvas.getContext('2d')
    canvas.width = width || imageData.width
    canvas.height = height || imageData.height
    ctx.putImageData(imageData, 0, 0)
  }
  function canvasGetImageData(canvas) {
    return canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height)
  }
  async function imageFromUrl(url) {
    const image = new Image()
    image.src = url
    await image.decode()
    return image
  }