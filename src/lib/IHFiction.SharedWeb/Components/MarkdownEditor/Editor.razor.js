export class BoundToast {
    editor = null;
    
    constructor(options, dotNetObjectReference) {
        this.dotNetObjectReference = dotNetObjectReference;

        options.events = {
            change: () => {
                const markdown = this.editor.getMarkdown();
                dotNetObjectReference.invokeMethodAsync('OnEditorContentChanged', markdown);
            }
        };
        options.hooks = {
            addImageBlobHook: (blob, callback) => {
                const reader = new FileReader();
                reader.onload = (event) => {
                    const base64Image = event.target.result;
                    // The callback inserts the image into the editor.
                    // The second argument is the alt text, which we can leave empty.
                    callback(base64Image, '');
                };
                reader.readAsDataURL(blob);
                return false; // We've handled the upload.
            }
        };

        this.editor = new toastui.Editor(options);
    }

    getMarkdown() {
        return this.editor.getMarkdown();
    }

    setMarkdown(markdown) {
        this.editor.setMarkdown(markdown);
    }

    destroy() {
        if (this.editor) {
            this.editor.destroy();
            this.editor = null;
        }
    }
}
