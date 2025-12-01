import { showToast } from './toast';

window.addCopyPlugin = () => {
    if (hljs._copyPluginAdded) return;
    hljs._copyPluginAdded = true;

    hljs.addPlugin({
        'after:highlightElement': ({ el, text }) => {
            const wrapper = el.parentElement;
            if (!wrapper || wrapper.querySelector('.copy-button')) return;

            wrapper.classList.add('relative');

            const copyButton = document.createElement('button');
            copyButton.className = 'p-2 text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 bx bx-copy text-xl cursor-pointer copy-button';
            copyButton.setAttribute('aria-label', 'Copy code to clipboard');
            copyButton.title = 'Copy code to clipboard';

            copyButton.onclick = () => {
                navigator.clipboard.writeText(text);
                showToast('The code block content has been copied to the clipboard.');
            };

            wrapper.prepend(copyButton);
        },
    });
};