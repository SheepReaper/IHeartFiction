export class BoundToast {
    editor = null;
    
    constructor(options, dotNetObjectReference) {
        this.dotNetObjectReference = dotNetObjectReference;

        // Debounce change events so very large single-paste payloads don't immediately
        // get sent over SignalR/Blazor server and cause disconnects.
        // We wait a short time after the last change before sending the content to .NET.
        this._changeTimer = null;
        const DEBOUNCE_MS = 250;

        options.events = {
            change: () => {
                const markdown = this.editor.getMarkdown();
                if (this._changeTimer) clearTimeout(this._changeTimer);
                this._changeTimer = setTimeout(() => {
                    // fire-and-forget the interop call
                    dotNetObjectReference.invokeMethodAsync('OnEditorContentChanged', markdown).catch(() => {
                        // swallow; .NET side logs errors separately
                    });
                }, DEBOUNCE_MS);
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
