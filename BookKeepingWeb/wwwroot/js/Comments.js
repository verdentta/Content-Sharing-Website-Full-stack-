document.addEventListener("DOMContentLoaded", function () {
    
    document.getElementById("add-comment-form").addEventListener("submit", async function (event) {
        event.preventDefault();

        const form = event.target;
        const uploadContentId = form.querySelector("input[name='uploadContentId']").value;
        const commentContent = form.querySelector("textarea[name='commentContent']").value;

        // Sanitize the input
        const sanitizedResult = sanitizeInput(commentContent);
        if (!sanitizedResult.isValid) {
            showToast(sanitizedResult.message || "Invalid comment!");
            return;
        }

        const sanitizedContent = sanitizedResult.content;
        if (!sanitizedContent) {
            showToast("Comment cannot be empty!");
            return;
        }

        try {
            const response = await fetch(form.action, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest",
                    "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({ uploadContentId, commentContent })
            });

            if (response.ok) {
                const result = await response.json();

                if (result.success) {
                    const commentsContainer = document.getElementById("comments-container");
                    const commentsList = document.getElementById("comments-list");
                    const noCommentsPlaceholder = document.getElementById("no-comments-placeholder");
                    

                    if (noCommentsPlaceholder) {
                        noCommentsPlaceholder.style.display = "none";
                        commentsList.style.display = "block";
                    }

                    const newComment = document.createElement("li");
                    newComment.className = "list-group-item new-comment-highlight"; // Add the highlight class
                    newComment.innerHTML = `
                                        <strong><a href="/Profile/PublicProfile?userId=${result.comment.userId}">
                                                ${result.comment.userName}
                                         </a></strong>
                                        <span class="comment-text">${result.comment.content}</span>
                                        <span class="text-muted" style="font-size: 0.9rem;">(${result.comment.date})</span>
                                        <button class="btn btn-link btn-sm reply-button" data-username="${result.comment.userName}">Reply</button>
                                    `;
                    commentsList.appendChild(newComment);

                    newComment.querySelector(".reply-button").addEventListener("click", handleReply);

                    

                    // Automatically scroll to the bottom of the comments container
                    commentsContainer.scrollTop = commentsContainer.scrollHeight;

                    form.querySelector("textarea[name='commentContent']").value = "";
                    // Redirect to the correct Details page
                    
                } else {
                    showToast(result.message || "Failed to add comment.");
                }
            } else if (response.status === 429) {
                try {
                    const result = await response.json();
                    if (result.redirectUrl) {
                        window.location.href = result.redirectUrl;
                    } else {
                        showToast(result.message || "⛔ You're commenting too fast.");
                    }
                } catch {
                    window.location.href = "/Home/RateLimit";
                }
            }
            else if (response.status === 401) {
                showToast("You need to be logged in to comment!");
            }
            else {
                showToast("⚠️ Failed to add comment. Please try again.");
            }
        } catch (error) {
            console.error("Error:", error);
            showToast("An unexpected error occurred. Please try again.");
        }
    });

    // New sanitization function
    function sanitizeInput(input) {
        // Step 1: Trim whitespace
        let sanitized = input.trim();

        // Step 2: Enforce 500-character limit
        if (sanitized.length > 500) {
            sanitized = sanitized.substring(0, 500);
        }

        // Step 3: Remove or escape HTML tags
        // Option 1: Strip all tags (simple approach)
        sanitized = sanitized.replace(/<[^>]*>/g, ""); // Removes <img>, <script>, etc.

        // Option 2: Escape HTML (mimics server HtmlEncode, keeps content visible)
         sanitized = sanitized.replace(/&/g, "&amp;")
                            .replace(/</g, "&lt;")
                             .replace(/>/g, "&gt;")
                             .replace(/"/g, "&quot;")
                             .replace(/'/g, "&#x27;");

        // Step 4: Check for malicious patterns (basic)
        const maliciousPatterns = /(on\w+=|javascript:|alert\()/i;
        if (maliciousPatterns.test(sanitized)) {
            return { isValid: false, content: null, message: "Scripts or malicious code are not allowed!" };
        }

        return { isValid: true, content: sanitized };
    }

    function handleReply() {
        const username = this.getAttribute("data-username");
        const textarea = document.querySelector("#add-comment-form textarea");

        if (!textarea.value.includes(`@${username}`)) {
            textarea.value = `@${username} ` + textarea.value;
        }

        textarea.focus();
    }

    document.querySelectorAll(".reply-button").forEach(button => {
        button.addEventListener("click", handleReply);
    });

    // Like button logic
    const likeButton = document.getElementById("like-button");
    if (likeButton) {
        likeButton.addEventListener("click", async function () {
            const contentId = likeButton.getAttribute("data-content-id");

           

            try {
                const response = await fetch('/Home/ToggleLike', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    },
                    body: JSON.stringify({ contentId: contentId })
                });

                if (response.ok) {
                    const result = await response.json();

                    

                    if (result.success) {

                        // Store the updated like count and status in sessionStorage
                        sessionStorage.setItem(`likedPost_${contentId}`, JSON.stringify({
                            likesCount: result.likesCount,
                            liked: result.liked
                        }));

                        // Add glow animation
                        likeButton.classList.add("glow");


                        // Remove the glow animation class after the animation finishes
                        setTimeout(() => {
                            likeButton.classList.remove("glow");
                        }, 600); // Match the duration of the animation


                        // Update the like count and button text
                        const likeCountSpan = document.getElementById("like-count");
                        

                        if (likeCountSpan) {
                            likeCountSpan.textContent = result.likesCount;
                        } 

                        if (result.liked) {
                            likeButton.classList.remove("btn-outline-primary");
                            likeButton.classList.add("btn-primary");
                            likeButton.textContent = `👍 Liked (${result.likesCount})`;
                        } else {
                            likeButton.classList.remove("btn-primary");
                            likeButton.classList.add("btn-outline-primary");
                            likeButton.textContent = `👍 Like (${result.likesCount})`;
                        }
                    } else {
                        showToast("Failed to update like status. Please try again.");
                    }
                } else {
                    showToast("You need to be logged in to like a post.");

                }
            } catch (error) {
                console.error("Error:", error);
                showToast("An unexpected error occurred.");
            }
        });
    }


});

document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".report-comment-button").forEach(button => {
        button.addEventListener("click", function () {
            var commentId = this.getAttribute("data-comment-id");

            if (!commentId) {
                alert("Error: Unable to retrieve comment ID.");
                return;
            }

            document.getElementById("reportCommentId").value = commentId; // ✅ Set the correct ID
            
        });
    });
});

document.getElementById("submitCommentReport").addEventListener("click", function () {
    var commentId = document.getElementById("reportCommentId").value.trim();
    var reportReason = document.getElementById("reportReason").value.trim();
    var additionalDetails = document.getElementById("reportDetails").value.trim();
    const errorText = document.getElementById("reportDescriptionError");

    if (!commentId) {
        alert("Error: Comment ID is missing. Please try again.");
        return;
    }

    if (!reportReason) {
        errorText.style.display = "block";
        return;
    } else {
        errorText.style.display = "none";
    }

    const reportData = {
        commentId: commentId,
        reportReason: reportReason,
        additionalDetails: additionalDetails || null
    };

    const modal = bootstrap.Modal.getInstance(document.getElementById("commentReportModal"));

    fetch("/Home/ReportComment", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify(reportData)
    })
        .then(async response => {
            // 🛑 Handle 429 (rate-limited)
            if (response.status === 429) {
                modal.hide();
                try {
                    const data = await response.json();
                    if (data.redirectUrl) {
                        window.location.href = data.redirectUrl;
                    } else {
                        showToast(data.message || "⛔ You're reporting too fast.");
                    }
                } catch {
                    window.location.href = "/Home/RateLimit";
                }
                return;
            }

            const data = await response.json();
            modal.hide();

            if (data.success) {
                showToast("✅ Comment reported successfully.");
                document.getElementById("commentReportForm").reset();
            } else {
                showToast(data.message || "❌ You need to be logged in to report.");
            }
        })
        .catch(error => {
            modal.hide();
            console.error("Error:", error);
            showToast("You need to be logged in to report a comment!");
        });
});
