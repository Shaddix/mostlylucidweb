import EasyMDE from "easymde";

export function codeeditor() {
    return {
        initialize: initialize,
        saveContentToDisk: saveContentToDisk,
        setupCodeEditor: setupCodeEditor,
        updateContent: updateContent,
        populateCategories: populateCategories,
        getinstance: getinstance
    }
}

function setupCodeEditor(elementId) {
    console.log('Page loaded without refresh');

    const easymde = initialize(elementId);
    // Trigger on change event of EasyMDE editor
    easymde.codemirror.on("keydown", function(instance, event) {
        let triggerUpdate = false;
        if ((event.ctrlKey || event.metaKey) && event.altKey && event.key.toLowerCase() === "r") {
            event.preventDefault();
            triggerUpdate = true;
        }
        if (event.key === "Enter") {
            triggerUpdate = true;
        }
        if (triggerUpdate) {
            updateContent(easymde);
        }
    });
}

function populateCategories(categories) {
    var categoriesDiv = document.getElementById('categories');
    categoriesDiv.innerHTML = '';

    categories.forEach(function(category) {
        let span = document.createElement('span');
        span.className = 'inline-block rounded-full dark bg-blue-dark px-2 py-1 font-body text-sm text-white outline-1 outline outline-green-dark dark:outline-white mr-2';
        span.textContent = category;
        categoriesDiv.appendChild(span);
    });
}

function updateContent(easymde) {
    var content = easymde.value();

    fetch('/api/editor/getcontent', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ content: content })
    })
        .then(response => response.json())
        .then(data => {
            const renderedContent = document.getElementById('renderedcontent');
            renderedContent.innerHTML = data.htmlContent;
            document.getElementById('title').innerHTML = data.title;
            const date = new Date(data.publishedDate);

            const formattedDate = new Intl.DateTimeFormat('en-GB', {
                weekday: 'long',
                day: 'numeric',
                month: 'long',
                year: 'numeric'
            }).format(date);

            document.getElementById('publishedDate').innerHTML = formattedDate;
            window.mostlylucid.simplemde.populateCategories(data.categories);

            mermaid.run();
            // Only highlight new code blocks in the updated content
            const codeBlocks = renderedContent.querySelectorAll('pre code:not(.hljs)');
            codeBlocks.forEach((block) => {
                hljs.highlightElement(block);
            });
        })
        .catch(error => console.error('Error:', error));
}

function initialize(elementId, reducedToolbar = false) {
    if (!window.simplemdeInstances) {
        window.simplemdeInstances = {};
    }

    const element = document.getElementById(elementId);
    if (!element) return;

    if (window.simplemdeInstances[elementId]) {
        window.simplemdeInstances[elementId].toTextArea();
        window.simplemdeInstances[elementId] = null;
    }

    let easymdeInstance = {};

    // Custom toolbar buttons using BoxIcons (Font Awesome not loaded)
    const toolbarButtons = {
        bold: {
            name: "bold",
            action: EasyMDE.toggleBold,
            className: "bx bx-bold",
            title: "Bold (Ctrl+B)"
        },
        italic: {
            name: "italic",
            action: EasyMDE.toggleItalic,
            className: "bx bx-italic",
            title: "Italic (Ctrl+I)"
        },
        heading: {
            name: "heading",
            action: EasyMDE.toggleHeadingSmaller,
            className: "bx bx-heading",
            title: "Heading (Ctrl+H)"
        },
        quote: {
            name: "quote",
            action: EasyMDE.toggleBlockquote,
            className: "bx bx-quote-left",
            title: "Quote (Ctrl+')"
        },
        unorderedList: {
            name: "unordered-list",
            action: EasyMDE.toggleUnorderedList,
            className: "bx bx-list-ul",
            title: "Unordered List (Ctrl+L)"
        },
        orderedList: {
            name: "ordered-list",
            action: EasyMDE.toggleOrderedList,
            className: "bx bx-list-ol",
            title: "Ordered List (Ctrl+Alt+L)"
        },
        link: {
            name: "link",
            action: EasyMDE.drawLink,
            className: "bx bx-link",
            title: "Insert Link (Ctrl+K)"
        },
        image: {
            name: "image",
            action: EasyMDE.drawImage,
            className: "bx bx-image",
            title: "Insert Image (Ctrl+Alt+I)"
        },
        code: {
            name: "code",
            action: EasyMDE.toggleCodeBlock,
            className: "bx bx-code-block",
            title: "Code Block (Ctrl+Alt+C)"
        },
        preview: {
            name: "preview",
            action: EasyMDE.togglePreview,
            className: "bx bx-show no-disable",
            title: "Toggle Preview (Ctrl+P)"
        },
        sideBySide: {
            name: "side-by-side",
            action: EasyMDE.toggleSideBySidePreview,
            className: "bx bx-columns no-disable",
            title: "Side by Side (F9)"
        },
        fullscreen: {
            name: "fullscreen",
            action: EasyMDE.toggleFullScreen,
            className: "bx bx-fullscreen no-disable",
            title: "Fullscreen (F11)"
        },
        save: {
            name: "save",
            action: function (editor) {
                var params = new URLSearchParams(window.location.search);
                var slug = params.get("slug");
                var language = params.get("language") || "en";
                saveContentToDisk(editor.value(), slug, language);
            },
            className: "bx bx-save",
            title: "Save to Disk"
        },
        insertCategory: {
            name: "insert-category",
            action: function (editor) {
                var category = prompt("Enter categories separated by commas", "EasyNMT, ASP.NET, C#");
                if (category) {
                    var currentContent = editor.value();
                    var categoryTag = `<!--category-- ${category} -->\n\n`;
                    editor.value(currentContent + categoryTag);
                }
            },
            className: "bx bx-purchase-tag",
            title: "Insert Categories"
        },
        update: {
            name: "update",
            action: function () {
                updateContent(getinstance(elementId));
            },
            className: "bx bx-refresh",
            title: "Update Preview (Ctrl+Alt+R)"
        },
        insertDatetime: {
            name: "insert-datetime",
            action: function (editor) {
                var now = new Date();
                var formattedDateTime = now.toISOString().slice(0, 16);
                var datetimeTag = `<datetime class="hidden">${formattedDateTime}</datetime>\n\n`;
                var currentContent = editor.value();
                editor.value(currentContent + datetimeTag);
            },
            className: "bx bx-calendar",
            title: "Insert Datetime"
        }
    };

    if (reducedToolbar) {
        easymdeInstance = new EasyMDE({
            element: element,
            toolbar: [
                toolbarButtons.bold, toolbarButtons.italic, toolbarButtons.heading, "|",
                toolbarButtons.quote, toolbarButtons.unorderedList, toolbarButtons.orderedList
            ]
        });
    } else {
        easymdeInstance = new EasyMDE({
            forceSync: true,
            renderingConfig: {
                singleLineBreaks: true,
                codeSyntaxHighlighting: true,
            },
            element: element,
            minHeight: "400px",
            toolbar: [
                toolbarButtons.bold, toolbarButtons.italic, toolbarButtons.heading, "|",
                toolbarButtons.quote, toolbarButtons.unorderedList, toolbarButtons.orderedList, "|",
                toolbarButtons.link, toolbarButtons.image, toolbarButtons.code, "|",
                toolbarButtons.save, toolbarButtons.insertCategory, toolbarButtons.update, toolbarButtons.insertDatetime, "|",
                toolbarButtons.preview, toolbarButtons.sideBySide, toolbarButtons.fullscreen
            ]
        });
    }

    window.simplemdeInstances[elementId] = easymdeInstance;
    return easymdeInstance;
}

function saveContentToDisk(content, slug, language) {
    console.log("Saving content to disk...");

    var filename = (slug || "untitled") + (language === 'en' ? ".md" : `.${language}.md`);
    var blob = new Blob([content], { type: "text/markdown;charset=utf-8;" });
    var link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = filename;

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    console.log("Download triggered for " + filename);
}

function getinstance(elementId) {
    return window.simplemdeInstances ? window.simplemdeInstances[elementId] : null;
}