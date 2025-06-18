const ul = document.querySelector(".list-view"),
    input = document.querySelector(".tag-input"),
    tagNum = document.getElementById("tag-remain");

const errSpan = document.getElementById("error-span");
const tagInput = document.getElementById("tagdata");
const maxFileSize = 200 * 1024 * 1024; // 200 MB
const alertSpan = document.getElementById("alert-files");

let maxTags = 10,
    tags = [];

function countTags() {
    tagNum.innerText = maxTags - tags.length;

    // Get selected predefined tags
    const selectedPredefinedTags = Array.from(document.querySelectorAll("input[name='PredefinedTags']:checked"))
        .map(cb => cb.value);

    // Store both user-defined and predefined tags in `tagdata`
    tagInput.value = JSON.stringify({
        predefinedTags: selectedPredefinedTags,
        userDefinedTags: tags.length > 0 ? tags : [] // If empty, still send an empty array
    });

    
}

function createTag() {
    ul.querySelectorAll("li").forEach(li => li.remove());
    tags.slice().reverse().forEach(tag => {
        let liTag = `<li class="bg-secondary li-tag">${tag}<i onclick="Remove(this, '${tag}')">x</i></li>`;
        ul.insertAdjacentHTML("afterbegin", liTag);
    });
    countTags();
}

function Remove(element, tag) {
    let index = tags.indexOf(tag);
    tags = tags.slice(0, index).concat(tags.slice(index + 1));
    element.parentElement.remove();
    countTags();
}

function AddPrev(list) {
    list.forEach(tag => {
        tags.push(tag);
        createTag();
    });
}

function addTag(e) {
    let tag = e.target.value.trim(); // Trim spaces for clean input
    if (e.inputType === "insertText" && e.data === " ") {
        e.preventDefault();
    }

    if (tag.length > 30) {
        e.target.value = "";
        errSpan.innerText = "Error: max length of a tag is 30 characters.";
        return;
    }

    if ((e.key === " " || e.key === "Enter" || e.inputType === "insertText" && e.data === " ") && tag.length > 1) {
        if (!tags.includes(tag) && tags.length < maxTags) {
            errSpan.innerText = "";
            tags.push(tag);
            createTag();
        }
        e.target.value = ""; // Clear input after adding tag
    }
}

function validatePredefinedTags() {
    const selectedPredefinedTags = Array.from(document.querySelectorAll("input[name='PredefinedTags']:checked"))
        .map(cb => cb.value);

    if (selectedPredefinedTags.length < 3) {
        return "Please select at least 3 predefined tags.";
    }

    errSpan.innerText = ""; // Clear error message if valid
    return "";
}

function validateTitle() {
    const titleInput = document.getElementById("titleInput");
    const title = titleInput.value.trim();
    if (!title) {
        return "Title is required.";
    }
    if (title.length > 90) {
        return "Title cannot exceed 90 characters.";
    }
    const titleErrorSpan = document.querySelector("span[data-valmsg-for='Title']");
    if (titleErrorSpan) titleErrorSpan.innerText = "";
    return "";
}

function validateFile() {
    const fileInput = document.getElementById("files");
    const files = fileInput.files;
    const allowedExtensions = [".jpg", ".jpeg", ".png", ".mp4"];

    if (files.length !== 1) {
        alertSpan.innerText = "";
        return "You must upload exactly one file.";
    }

    const file = files[0];
    const fileExtension = "." + file.name.split(".").pop().toLowerCase();
    if (!allowedExtensions.includes(fileExtension)) {
        alertSpan.innerText = "";
        return `${file.name} is not a supported format. Allowed: JPG, JPEG, PNG, MP4.`;
    }

    if (file.size > maxFileSize) {
        alertSpan.innerText = "";
        return `The file "${file.name}" exceeds the 200MB size limit.`;
    }

    alertSpan.innerText = "";
    return "";
}

function validateFiles() {
    const alertSpan = document.getElementById("alert-files");
    alertSpan.textContent = ""; // Clear any previous messages

    const input = document.getElementById("files");
    const files = input.files;

    if (files.length !== 1) {
        showErrorForTags(["You must upload exactly 1 file."]);
        input.value = ""; // Clear file input
        return;
    }

    const file = files[0];
    const allowedExtensions = [".gif", ".jpg", ".jpeg", ".png", ".mp4"];
    const fileExtension = file.name.split('.').pop().toLowerCase();

    if (file.size > maxFileSize) {
        showErrorForTags([`The file "${file.name}" exceeds the 200MB size limit.`]);
        input.value = ""; // Clear file input
        return;
    }

    if (!allowedExtensions.includes("." + fileExtension)) {
        showErrorForTags([`"${file.name}" is not a supported format.\nAllowed: GIF, JPG, JPEG, PNG, MP4.`]);
        input.value = ""; // Clear file input
        return;
    }

    alertSpan.textContent = ""; // No errors
}

function showError(messages) {
    const errorMessages = Array.isArray(messages) ? messages : [messages];
    const formattedMessage = errorMessages.join("\n") + "\n\nNeed to convert your file? Try https://convertio.co/";
    document.getElementById("errorModalText").innerText = formattedMessage;
    const errorModal = new bootstrap.Modal(document.getElementById("errorModal"));
    errorModal.show();
}

function showErrorForTags(messages) {
    const errorMessages = Array.isArray(messages) ? messages : [messages];
    const formattedMessage = errorMessages.join("\n");
    document.getElementById("errorModalText").innerText = formattedMessage;
    const errorModal = new bootstrap.Modal(document.getElementById("errorModal"));
    errorModal.show();

    // Ensure the modal backdrop is removed when the modal is hidden
    document.getElementById("errorModal").addEventListener("hidden.bs.modal", function () {
        document.querySelectorAll(".modal-backdrop").forEach(backdrop => backdrop.remove());
        document.body.classList.remove("modal-open");
        document.body.style.overflow = "auto"; // Restore scrolling
        document.body.style.paddingRight = ""; // Remove any padding added by Bootstrap
    }, { once: true }); // Ensure the event listener is only called once per modal show
}

input.addEventListener("input", addTag);
input.addEventListener("keydown", function (e) {
    if (e.key === " " || e.key === "Enter") {
        e.preventDefault();
        addTag(e);
    }
});

if (typeof tagliststring !== "undefined" && Array.isArray(tagliststring)) {
    AddPrev(tagliststring);
}

const removeBtn = document.querySelector(".remove-button");
if (removeBtn) {
    removeBtn.addEventListener("click", () => {
        tags.length = 0;
        ul.querySelectorAll("li").forEach(li => li.remove());
        countTags();
    });
}

const uploadForm = document.getElementById("uploadForm");
if (uploadForm) {
    uploadForm.addEventListener("submit", function (event)
    {
    event.preventDefault();
    document.activeElement.blur();

    // Collect all validation errors
    const errors = [];
    const titleError = validateTitle();
    const fileError = validateFile();
    const tagsError = validatePredefinedTags();

    if (titleError) errors.push(titleError);
    if (fileError) errors.push(fileError);
    if (tagsError) errors.push(tagsError);

    if (errors.length > 0) {
        showErrorForTags(errors);
        document.getElementById("submit-btn").disabled = false;
        return;
    }

    document.getElementById("submit-btn").disabled = true;
    countTags();

    document.getElementById("progress-container").style.display = "block";
    const progressBar = document.getElementById("progress-bar");
    const progressText = document.getElementById("progress-text");

    const form = document.getElementById("uploadForm");
    const formData = new FormData(form);

    const xhr = new XMLHttpRequest();
    xhr.open("POST", form.action, true);

    xhr.upload.onprogress = function (event) {
        if (event.lengthComputable) {
            const percentComplete = Math.round((event.loaded / event.total) * 100);
            if (percentComplete < 100) {
                progressBar.style.width = percentComplete + "%";
                progressBar.setAttribute("aria-valuenow", percentComplete);
                progressText.innerText = "Uploading... " + percentComplete + "%";
            } else {
                progressBar.style.width = "99%";
                progressText.innerText = "Uploading...";
            }
        }
    };

    xhr.onload = function () {
        if (xhr.status === 200) {
            progressBar.style.width = "100%";
            progressBar.classList.add("bg-success");
            progressText.innerText = "Upload Complete!";
            setTimeout(() => {
                window.location.href = "/Profile/Display";
            }, 1000);
        } else if (xhr.status === 429) {
            try {
                const response = JSON.parse(xhr.responseText);
                if (response.redirectUrl) {
                    window.location.href = response.redirectUrl;
                } else {
                    progressBar.style.width = "0%";
                    progressBar.classList.add("bg-danger");
                    progressText.innerText = "Upload Failed!";
                    showErrorForTags([response.message || "You're uploading too fast! Please wait and try again."]);
                    document.getElementById("submit-btn").disabled = false;
                }
            } catch (e) {
                progressBar.style.width = "0%";
                progressBar.classList.add("bg-danger");
                progressText.innerText = "Upload Failed!";
                document.getElementById("submit-btn").disabled = false;
                setTimeout(() => {
                    window.location.href = "/Home/RateLimit";
                }, 1000);
            }
        } else {
            progressBar.style.width = "0%";
            progressBar.classList.add("bg-danger");
            progressText.innerText = "Upload Failed!";
            try {
                const response = JSON.parse(xhr.responseText);
                showErrorForTags([response.message || "Upload failed. Please check the form and try again."]);
            } catch (e) {
                showErrorForTags(["Upload failed. Please try again."]);
            }
            document.getElementById("submit-btn").disabled = false;
        }
    };

    xhr.onerror = function () {
        progressText.innerText = "Error during upload!";
        progressBar.classList.add("bg-danger");
        document.getElementById("submit-btn").disabled = false;
    };

    xhr.send(formData);
});
}

document.querySelectorAll("input[name='PredefinedTags']").forEach(cb => {
    cb.addEventListener("change", countTags);
});