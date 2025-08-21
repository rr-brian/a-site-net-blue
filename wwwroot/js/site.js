// Global site functionality
console.log('RAI - RTS AI Toolbox initialized');

// Handle session timeout
let sessionTimeout;
const SESSION_TIMEOUT = 30 * 60 * 1000; // 30 minutes

function resetSessionTimeout() {
    clearTimeout(sessionTimeout);
    sessionTimeout = setTimeout(() => {
        alert('Your session has expired. Please refresh the page to continue.');
        window.location.reload();
    }, SESSION_TIMEOUT);
}

// Reset timeout on user activity
document.addEventListener('click', resetSessionTimeout);
document.addEventListener('keypress', resetSessionTimeout);
document.addEventListener('scroll', resetSessionTimeout);

// Initialize session timeout
resetSessionTimeout();