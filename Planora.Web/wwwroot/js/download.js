// Triggers a client-side file download from in-memory text (used by the data export on the profile
// page). The API returns the export JSON over the authenticated HttpClient, so we build the file in
// the browser rather than navigating to the endpoint (which would not carry the bearer token).
window.planoraDownloadFile = function (fileName, mimeType, content) {
    const blob = new Blob([content], { type: mimeType || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName || 'download';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};
