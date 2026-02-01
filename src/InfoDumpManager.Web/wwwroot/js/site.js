// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener("DOMContentLoaded", () => {
	document.querySelectorAll("iframe[data-snapshot]").forEach((frame) => {
		const encoded = frame.getAttribute("data-snapshot");
		if (!encoded) {
			return;
		}

		try {
			frame.srcdoc = window.atob(encoded);
		} catch (error) {
			console.error("Failed to decode snapshot content.", error);
		}
	});

	document.querySelectorAll("[data-confirm]").forEach((button) => {
		button.addEventListener("click", (event) => {
			const message = button.getAttribute("data-confirm");
			if (message && !window.confirm(message)) {
				event.preventDefault();
			}
		});
	});
});
