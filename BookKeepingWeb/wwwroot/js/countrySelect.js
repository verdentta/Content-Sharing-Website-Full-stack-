const container = document.getElementById("country-selector");

async function getCountries() {
    try {
        const response = await fetch('/data/CountrySelector.json');
        const data = await response.json();

        const countries = data.map(country => country.name.common).sort((a, b) => a.localeCompare(b));
        let currentCountry = container.getAttribute("data-current-country") || "None"; // Fallback to "None"

        // Clear existing options except the default "None"
        while (container.options.length > 1) {
            container.remove(1);
        }

        countries.forEach(country => {
            const option = document.createElement("option");
            option.value = country;
            option.textContent = country;
            if (country === currentCountry) {
                option.selected = true; // Ensure it stays selected
            }
            container.appendChild(option);
        });

        // ✅ Ensure the selected country stays in session storage to persist across reloads
        sessionStorage.setItem("selectedCountry", currentCountry);

    } catch (error) {
        console.error("Error fetching countries:", error);
    }
}

// ✅ Restore the country from session storage on page load
document.addEventListener("DOMContentLoaded", () => {
    const storedCountry = sessionStorage.getItem("selectedCountry");
    if (storedCountry) {
        document.getElementById("country-selector").value = storedCountry;
    }
});

getCountries();
