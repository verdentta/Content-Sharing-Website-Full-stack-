let page = 1; // Current page
let noMoreContent = false;
let isTransitioning = false; // Prevent multiple transitions at once
const contentContainer = document.getElementById("content-container");
const overlay = document.getElementById("detail-overlay");
const overlayDetails = document.getElementById("overlay-details");
const currentPost = document.getElementById("current-post");
const card = document.getElementById("overlay-card");
let isOverlayActive = false; // Flag to track if overlay is active

let initialLoad = true; // Ensure most recent posts on the first load

// Disable post transitions by removing the scroll event listener

document.addEventListener("click", function (event) {
    if (event.target.classList.contains("flag-button")) {
        const postId = event.target.getAttribute("data-post-id");
        if (!postId) {
            console.error("❌ Error: Post ID is missing.");
            return;
        }

        // ✅ Set the Post ID inside the hidden input field in the modal
        document.getElementById("takedownPostId").value = postId;

        // ✅ Open the modal (since it's outside the dynamically loaded content)
        var takedownModal = new bootstrap.Modal(document.getElementById("takedownModal"));
        takedownModal.show();
    }
});

function openTakedownModal(postId) {
    document.getElementById("takedownPostId").value = postId; // ✅ Set Post ID
    var takedownModal = new bootstrap.Modal(document.getElementById("takedownModal"));
    takedownModal.show();
}

function disablePostTransitions() {
    document.removeEventListener("wheel", handleScroll);
}

// Re-enable post transitions by adding the scroll event listener back
function enablePostTransitions() {
    document.addEventListener("wheel", handleScroll);
}

// Function to load a single post
async function loadSinglePost(direction = "down") {
    if (isTransitioning) return; // Prevent multiple transitions
    isTransitioning = true;

    const currentPostElement = currentPost.firstElementChild;

    // Determine the animation based on direction
    if (currentPostElement) {
        currentPostElement.style.animationName =
            direction === "down" ? "slideOutUp" : "slideOutDown";
    }

    // Adjust page number only after ensuring it's not the initial load
    const nextPage = direction === "down"
        ? initialLoad ? page : page + 1
        : Math.max(1, page - 1);

    // Prevent scrolling past the last content
    if (direction === "down" && noMoreContent) {
        // Trigger slide-out and slide-in animations for the current post
        if (currentPostElement) {
            currentPostElement.addEventListener(
                "animationend",
                () => {
                    // 🚀 Stop and remove all videos before replacing content
                    document.querySelectorAll("video").forEach((video) => {
                        video.pause();
                        video.src = "";
                        video.load();
                        video.remove();
                    });

                    currentPost.replaceChildren(newPost); // Replace content after animation ends
                    page = nextPage; // Update page number
                    initialLoad = false; // Disable initial load
                    isTransitioning = false;
                },
                { once: true } // Ensure the listener runs only once
            );
        }
        return;
    }

    // Reset noMoreContent when scrolling up
    if (direction === "up" && nextPage < page) {
        noMoreContent = false;
    }

    // Fetch the next post
    const requestPage = initialLoad ? 1 : nextPage;

    try {
        const response = await fetch(`/Home/GetContent?page=${requestPage}&pageSize=1`);
        if (!response.ok) {
            throw new Error(`Error fetching content: ${response.statusText}`);
        }

        const data = await response.json();

        if (!data.contents || data.contents.length === 0) {
            if (direction === "down") {
                noMoreContent = true;

                // Slide the last post back in if trying to scroll past it
                if (currentPostElement) {
                    currentPostElement.addEventListener(
                        "animationend",
                        () => {
                            currentPostElement.style.animationName = "slideInDown"; // Slide it back in
                            isTransitioning = false;
                        },
                        { once: true }
                    );
                }
                return;
            }
        } else {
            noMoreContent = false; // Reset when valid content is fetched
        }

        const content = data.contents[0];

        console.log("Full content response:", content); // 🔥 Debug full response

        // Ensure we are accessing the property correctly
        console.log(`Post ${content.id} liked by user?`, content.likedByUser);

        const isLiked = content.likedByUser; // ✅ Get the liked state from backend
        console.log(`Post ${content.id} liked by user?`, isLiked); // 🔍 Debugging log
        const likeButtonClass = isLiked ? "btn-primary" : "btn-outline-primary";
        const likeButtonText = isLiked ? `👍 Liked` : `👍 Like`;
        // ❌ Don't apply the glow-effect on page reload
        const likeButtonGlow = ""; 

         const thumbnailUrl = content.thumbnailPath || "/images/default-thumbnail.jpg"; 

        // Create the new post container
        const newPost = document.createElement("div");
        newPost.classList.add("post-container");
        newPost.style.animationName =
            direction === "down" ? "slideInUp" : "slideInUp";
        newPost.style.animationName =
            direction === "up" ? "slideInDown" : "slideInUp";
        // Apply slide-in animation
        newPost.innerHTML = `
            <div class="card" 
                 style="width: 100%; box-shadow: 0 4px 8px rgba(0,0,0,0.2); border-radius: 10px;">
                <div style="position: relative; display: flex; justify-content: center; align-items: center; background-color: #6f42c1 overflow: hidden;">
                    ${
            content.contentType === 0 || content.contentType === 2
                ? `<img src="${content.contentPath}" alt="${content.title}" style="max-height: 100%; max-width: 100%; object-fit: cover;" />`
                : `
      <div class="video-wrapper" style="position: relative; display: inline-block;">
          <img src="${content.thumbnailPath}" data-video="${content.contentPath}" class="video-thumbnail" alt="Video Thumbnail" 
               style="max-height: 100%; max-width: 100%; object-fit: contain; cursor: pointer;" />
          <div class="play-button"></div>
              
          </div>
      </div>
    `
}
                </div>
                <div class="card-body text-center">
                    <h5 class="card-title">${content.title}</h5>

                              <!-- Like, Share, Comment Buttons -->
                    <div class="d-flex justify-content-center gap-2">
                        <!-- Like Button -->
                        <button class="btn ${likeButtonClass} like-button ${likeButtonGlow}"
                            data-content-id="${content.id}" 
                            id="like-button-${content.id}">
                        👍 (<span id="like-count-${content.id}">${content.likes ?? 0}</span>)
                    </button>

                        <!-- Share Button -->
                        <button class="btn btn-outline-secondary share-button" onclick="copyLinkToClipboard('${content.id}')">
                            🔗 
                        </button>

                        <!-- Comment Button (Redirects & Scrolls to Comment Section) -->
                        <button class="btn btn-outline-info comment-button" onclick="redirectToComment('${content.id}')">
                            💬 
                        </button>

                        <button class="btn btn-warning flag-button" data-post-id="${content.id}">
                            🚩 
                        </button>

                        <button class="btn btn-primary view-more-btn" onclick="navigateToDetails('${content.id}')">
                            👁
                        </button>
                    </div>

                     <!-- View More Button -->
                    
                    <p class="card-text"><small class="text-muted"></small></p>
                    
                </div>
            </div>
        `;

        // Handle the animation end for the current post
        if (currentPostElement) {
            currentPostElement.addEventListener(
                "animationend",
                () => {
                    currentPost.replaceChildren(newPost); // Replace content after animation ends
                    page = nextPage; // Update page number
                    initialLoad = false; // Disable initial load
                    isTransitioning = false;
                },
                { once: true } // Ensure the listener runs only once
            );
        } else {
            currentPost.replaceChildren(newPost);
            page = nextPage;
            initialLoad = false;
            isTransitioning = false;
        }
    } catch (error) {
        console.error("Error loading content:", error);
        isTransitioning = false;
    }
}

document.addEventListener("click", function (event) {
    const videoWrapper = event.target.closest(".video-wrapper"); // Find the nearest video container
    if (!videoWrapper) return;

    const playButton = videoWrapper.querySelector(".play-button");
    const thumbnail = videoWrapper.querySelector(".video-thumbnail");
    const videoSrc = thumbnail.getAttribute("data-video");

    if (!videoSrc) return;

    // Create the video element
    const videoElement = document.createElement("video");
    videoElement.setAttribute("controls", "true");
    videoElement.setAttribute("autoplay", "true");
    videoElement.setAttribute("style", "max-height: 100%; max-width: 100%; object-fit: contain;");
    videoElement.innerHTML = `<source src="${videoSrc}" type="video/mp4" /> Your browser does not support the video tag.`;

    // Replace the thumbnail with the video
    videoWrapper.replaceChild(videoElement, thumbnail);
    playButton.remove(); // Remove the play button
});

// Function to Copy Link to Clipboard
function copyLinkToClipboard(postId) {
    const shareLink = `${window.location.origin}/Home/Details/${postId}`;
    navigator.clipboard.writeText(shareLink).then(() => {
        // Show Bootstrap Toast (Or Replace with Alert)
        var toastElement = document.getElementById("copyToast");
        if (toastElement) {
            var toast = new bootstrap.Toast(toastElement, { delay: 2000 });
            toast.show();
        } else {
            showToast("🔴 Link copied to clipboard!");
        }
    }).catch(err => console.error("Error copying link:", err));
}

// Function to Redirect to Comment Section in Details Page
function redirectToComment(postId) {
    sessionStorage.setItem("scrollToComment", "true"); // Store intent in session
    window.location.href = `/Home/Details/${postId}`; // Redirect to Details Page
}

// Handle scroll events
let scrollThreshold = 100; // change this value to change the sensitivey of the scroll
let scrollAccumulator = 0; // Accumulator to track total scroll

function handleScroll(event) {
    if (isTransitioning || isOverlayActive) return; // Ignore scrolls during transitions

    // Stop videos from playing when scrolling away
    document.querySelectorAll("video").forEach((video) => {
        if (!video.paused) {
            video.pause(); // Pause playback
        }
        video.src = "";  // Clear the video source to immediately stop buffering
        video.load();     // Reset the video element to stop any further requests
        video.remove();   // Remove the element from the DOM
    });

    // Accumulate the scroll delta
    scrollAccumulator += event.deltaY;

    // Check if the scroll threshold is met
    if (Math.abs(scrollAccumulator) >= scrollThreshold) {
        const direction = scrollAccumulator > 0 ? "down" : "up"; // Determine scroll direction

        // Reset accumulator after the threshold is reached
        scrollAccumulator = 0;

        // Allow scrolling up even if we're at the last post
        if (direction === "up" || !noMoreContent) {
            loadSinglePost(direction);
        }
    }
}



// Attach scroll event listener
document.addEventListener("wheel", handleScroll);


document.addEventListener("click", async function (event) {
    if (event.target.classList.contains("like-button")) {
        const likeButton = event.target;
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
                    const likeCountSpan = document.getElementById(`like-count-${contentId}`);

                    if (likeCountSpan) {
                        likeCountSpan.textContent = result.likesCount;
                    }

                    // 🔥 Apply Glow Effect
                    likeButton.classList.add("glow-effect");

                    // Remove glow effect after animation completes
                    setTimeout(() => {
                        likeButton.classList.remove("glow-effect");
                    }, 600);

                    // Toggle button appearance
                    if (result.liked) {
                        likeButton.classList.remove("btn-outline-primary");
                        likeButton.classList.add("btn-primary");
                        
                    } else {
                        likeButton.classList.remove("btn-primary");
                        likeButton.classList.add("btn-outline-primary");
                        
                    }
                } else {
                    showToast("Failed to update like status. Please try again.");                   
                }
            } else {
                showToast("You need to be logged in to like a post.");
            }
        } catch (error) {
            console.error("Error:", error);
            showToast("You need to be logged in to Like Posts!");
        }
    }
});

//this is added so that it only stores it in the session storage when the view button is clicked only, everything else, it will reset 
document.addEventListener("click", (event) => {
    if (event.target.classList.contains("view-more-btn") || event.target.classList.contains("comment-button")) {
        // Save the current post state
        sessionStorage.setItem("currentPostPage", page);
        sessionStorage.setItem("currentPostContent", currentPost.innerHTML);
    } else {
        // Clear session storage if the user clicks on anything else
        sessionStorage.removeItem("currentPostPage");
        sessionStorage.removeItem("currentPostContent");
    }
});

document.addEventListener("DOMContentLoaded", async () => {
    const savedPage = sessionStorage.getItem("currentPostPage");
    const savedContent = sessionStorage.getItem("currentPostContent");
    const lastViewedPostId = sessionStorage.getItem("lastViewedPostId");

    if (lastViewedPostId) {
        // Force fresh fetch for the last viewed post
        sessionStorage.removeItem("lastViewedPostId");
        await loadSinglePost(); // Fetch updated data
    } else if (savedPage && savedContent) {
        // Restore the saved post content and page
        page = parseInt(savedPage, 10);
        currentPost.innerHTML = savedContent;
        initialLoad = false; // Disable initial load logic

        // 🔥 Check if any likes changed in `details.cshtml` and update UI
        updateLikesFromSession();
    } else {
        // If no saved state, load the first post
        await loadSinglePost();
    }
});

// 🔥 Function to Update Like Counts from `sessionStorage`
function updateLikesFromSession() {
    document.querySelectorAll(".like-button").forEach(button => {
        const contentId = button.getAttribute("data-content-id");
        if (!contentId) {
            console.error("❌ Missing contentId in updateLikesFromSession", button);
            return;
        }

        const storedLikeData = sessionStorage.getItem(`likedPost_${contentId}`);
        if (storedLikeData) {
            const { likesCount, liked } = JSON.parse(storedLikeData);

            // Delay the update slightly to allow DOM elements to exist
            setTimeout(() => updateLikeButtonUI(button, likesCount, liked), 200);
        }
    });
}

function navigateToDetails(contentId) {
    window.location.href = `/Home/Details/${contentId}`;
}

// 🔥 Function to Update UI for Like Button
function updateLikeButtonUI(button, likesCount, liked) {
    const contentId = button.getAttribute("data-content-id");
    if (!contentId) {
        console.error("❌ contentId is missing in updateLikeButtonUI", button);
        return;
    }

    // 🔍 Check if the like count span exists
    const likeCountSpan = document.getElementById(`like-count-${contentId}`);
    if (!likeCountSpan) {
        console.warn(`⚠️ like-count-${contentId} element is missing in the DOM`);
    } else {
        likeCountSpan.textContent = likesCount; // ✅ Update count only if it exists
    }

    // Toggle class based on like state
    if (liked) {
        button.classList.remove("btn-outline-primary");
        button.classList.add("btn-primary");
    } else {
        button.classList.remove("btn-primary");
        button.classList.add("btn-outline-primary");
    }

    // ✅ Ensure the button always displays ONLY the thumbs-up icon and count
    button.innerHTML = `<span>👍</span> <span id="like-count-${contentId}">${likesCount}</span>`;

    // 🔥 Store the like state in sessionStorage
    sessionStorage.setItem(`likedPost_${contentId}`, JSON.stringify({ likesCount, liked }));
}



document.getElementById("submitTakedownRequest").addEventListener("click", async function () {
    
    const postId = document.getElementById("takedownPostId").value;
    const email = document.getElementById("takedownEmail").value;
    const description = document.getElementById("takedownDescription").value;
    const errorText = document.getElementById("descriptionError");

    // ✅ Show inline error message if the description field is empty
    if (!description) {
        errorText.style.display = "block"; // Show the error message
        return;
    } else {
        errorText.style.display = "none"; // Hide error message if valid
    }

    try {
        const response = await fetch('/Admin/SubmitTakedownRequest', { // 🔥 Using Admin Controller
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({ postId, email, description })
        });

        const result = await response.json();

        if (response.ok && result.success) {
            showToast(result.message);
            var modal = bootstrap.Modal.getInstance(document.getElementById("takedownModal"));
            modal.hide(); // Close the modal after successful submission
            document.getElementById("takedownRequestForm").reset(); // Clear the form
        } else {
            showToast("❌ Failed to submit request. Please try again.");
        }
    } catch (error) {
        console.error("Error:", error);
        errorText.textContent = "❌ Something went wrong. Please try again.";
        errorText.style.display = "block";

    }
});



let touchStartY = 0;
let touchEndY = 0;
const swipeThreshold = 50; // Minimum swipe distance in pixels

let isModalActive = false; // Add this variable if you haven't already

// Detect touch start position
document.addEventListener("touchstart", (event) => {
    if (isModalActive) return;
    touchStartY = event.touches[0].clientY;
});

// Detect touch end position and determine swipe direction and also prevent swiping when they press the flag button 
document.addEventListener("touchend", (event) => {
    if (isModalActive) return;
    touchEndY = event.changedTouches[0].clientY;
    handleSwipeGesture();
});

// Function to handle swipe gestures
function handleSwipeGesture() {
    let swipeDistance = touchEndY - touchStartY;

    if (Math.abs(swipeDistance) > swipeThreshold) {
        // 🚀 Stop and remove all videos before loading new content
        document.querySelectorAll("video").forEach((video) => {
            if (!video.paused) {
                video.pause(); // Pause playback
            }
            video.src = "";  // Clear the video source to immediately stop buffering
            video.load();     // Reset the video element to stop any further requests
            video.remove();   // Remove the element from the DOM
        });

        if (swipeDistance > 0) {
            // Swipe down detected
            console.log("📉 Swipe Down - Load Previous Post");
            loadSinglePost("up");
        } else {
            // Swipe up detected
            console.log("📈 Swipe Up - Load Next Post");
            loadSinglePost("down");
        }
    }
}


// Modal event listeners to control scrolling so that when the user pressed the flag button, it stops scrolling
const takedownModal = document.getElementById("takedownModal");
takedownModal.addEventListener("show.bs.modal", () => {
    isModalActive = true;
    disablePostTransitions(); // Disable scrolling when modal opens
    takedownModal.removeAttribute("aria-hidden");
});

takedownModal.addEventListener("hidden.bs.modal", () => {
    isModalActive = false;
    enablePostTransitions(); // Re-enable scrolling when modal closes
    takedownModal.setAttribute("aria-hidden", "true");
});


