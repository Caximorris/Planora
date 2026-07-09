// Instagram-style cover cropper built on the vendored Cropper.js. A fixed 16:9 frame; the user
// drags the image to reposition and zooms with the slider. planoraCropperGetResult rasterizes the
// selected region to a JPEG data URL that the board upload endpoint then stores — so the saved cover
// is already cropped and the board fills it with no distortion.
let _cropper = null;

export function init(imgEl, dataUrl) {
    destroy();
    imgEl.src = dataUrl;
    _cropper = new Cropper(imgEl, {
        aspectRatio: 16 / 9,
        viewMode: 1,            // keep the image covering the crop box
        dragMode: 'move',       // drag moves the image; the crop frame stays put
        cropBoxMovable: false,
        cropBoxResizable: false,
        toggleDragModeOnDblclick: false,
        autoCropArea: 1,
        background: false,
        minContainerHeight: 320,
    });
}

// slider value 0..100 → zoom relative to the fitted image (1x .. 3x)
export function zoom(percent) {
    if (!_cropper) return;
    _cropper.zoomTo(1 + (percent / 100) * 2);
}

export function getResult() {
    if (!_cropper) return null;
    const canvas = _cropper.getCroppedCanvas({
        width: 1600,
        height: 900,
        imageSmoothingQuality: 'high',
        fillColor: '#fff',
    });
    return canvas ? canvas.toDataURL('image/jpeg', 0.9) : null;
}

export function destroy() {
    if (_cropper) { _cropper.destroy(); _cropper = null; }
}
