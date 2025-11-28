
 export function submitTranslation() {
    console.log('submitTranslation called');

    const languageDropDown = document.getElementById('LanguageDropDown');
    if (!languageDropDown) {
        console.error('Language dropdown element not found');
        window.showToast && window.showToast('Language dropdown not found', 'error');
        return;
    }

    const translateEditor = 'markdowneditor';

    // Access Alpine.js data
    let shortCode = '';
    try {
        const alpineData = Alpine.$data(languageDropDown);
        shortCode = alpineData?.selectedShortCode || '';
        console.log('Selected language:', shortCode);
    } catch (e) {
        console.error('Failed to get Alpine data:', e);
        window.showToast && window.showToast('Failed to get language selection', 'error');
        return;
    }

    const mdeInstance = window.mostlylucid.simplemde.getinstance(translateEditor);
    if (!mdeInstance) {
        console.error('EasyMDE instance not found for:', translateEditor);
        window.showToast && window.showToast('Editor not initialized', 'error');
        return;
    }

    const markdown = mdeInstance.value();
    console.log('Markdown length:', markdown?.length || 0);

    if (!shortCode || shortCode === 'en') {
        console.warn('Please select a target language (not English)');
        window.showToast && window.showToast('Please select a target language', 'warning');
        return;
    }

    if (!markdown || markdown.trim() === '') {
        console.warn('No markdown content to translate');
        window.showToast && window.showToast('No content to translate', 'warning');
        return;
    }

    // Clear any previous translated content before starting new translation
    const translatedContent = document.getElementById('translatedcontent');
    if (translatedContent) {
        translatedContent.classList.add('hidden');
        const translatedMde = window.mostlylucid.simplemde.getinstance('translatedcontentarea');
        if (translatedMde) translatedMde.value('');
    }
    
    // Create the data object that matches your model
    const model = {
        Language: shortCode,
        OriginalMarkdown: markdown
    };

    // Show loading state
    window.showToast && window.showToast('Starting translation...', 'info');

    // Perform the fetch request to start the translation using POST
    fetch('/api/translate/start-translation', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(model)
    })
        .then(function(response) {
            if (response.ok) {
                return response.json();
            } else {
                throw new Error(`Translation failed: ${response.status} ${response.statusText}`);
            }
        })
        .then(function(taskId) {
            if (taskId) {
                console.log("Task ID:", taskId);
                window.showToast && window.showToast('Translation started', 'success');

                // Trigger an HTMX request to get the translations after saving
                htmx.ajax('get', "/editor/get-translations", {
                    target: '#translations',
                    swap: 'innerHTML',
                }).then(function () {
                    document.getElementById('translations').classList.remove('hidden');
                });
            }
        })
        .catch(function(error) {
            console.error('Translation error:', error);
            window.showToast && window.showToast('Translation failed: ' + error.message, 'error');
        });
}

export function viewTranslation(taskId) {

    // Construct the URL with the query parameters
    const url = `/api/translate/get-translation/${taskId}`;

    // Fetch call to the API endpoint
    fetch(url, {
        method: 'GET',
        headers: {
            'Accept': 'application/json'  // Indicate that we expect a JSON response
        }
    })
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }
            return response.json();
        })
        .then(data =>
 
        {
            const translateEditor = 'markdowneditor';
            let translatedContentArea = document.getElementById("translatedcontent")
            translatedContentArea.classList.remove("hidden");
            let textArea = document.getElementById('translatedcontentarea');
            let originalMde = window.mostlylucid.simplemde.getinstance('translatedcontentarea');
            if(!originalMde)
            {
                originalMde= window.mostlylucid.simplemde.initialize('translatedcontentarea', true);
             
            }
            originalMde.value(data.originalMarkdown)
            const mde = window.mostlylucid.simplemde.getinstance(translateEditor);
            mde.value(data.translatedMarkdown);
            window.mostlylucid.simplemde.updateContent(mde);
        })  // Log the successful response data
        .catch(error => console.error('Error:', error));  // Handle any errors
}