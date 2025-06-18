
document.addEventListener('DOMContentLoaded', () => {
    const searchIconButton = document.querySelector('.search-icon-button');
    const searchInput = document.querySelector('#search'); // Adjust this selector to match your input field ID or class.

    if (searchIconButton && searchInput) {
        searchIconButton.addEventListener('click', () => {
            searchInput.focus(); // Automatically focus on the search input field
        });
    }
});
