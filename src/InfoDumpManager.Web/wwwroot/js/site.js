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

	document.querySelectorAll("form").forEach((form) => {
		form.addEventListener("submit", () => {
			if (form.dataset.isSubmitting === "true") {
				return;
			}

			form.dataset.isSubmitting = "true";

			form.querySelectorAll('button[type="submit"]').forEach((button) => {
				if (!(button instanceof HTMLButtonElement)) {
					return;
				}

				if (!button.dataset.originalLabel) {
					button.dataset.originalLabel = button.innerHTML;
				}

				const processingText = button.dataset.processingText || "Processing...";
				button.disabled = true;
				button.setAttribute("aria-busy", "true");
				button.innerHTML =
					'<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>' +
					`<span>${processingText}</span>`;
			});
		});
	});
});
