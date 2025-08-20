/**
 * Authentication module using MSAL.js for Entra ID integration
 */

// Default MSAL configuration - will be replaced with values from the server
let msalConfig = {
    auth: {
        clientId: '', // Will be populated from server config
        authority: '', // Will be populated from server config
        redirectUri: window.location.origin // Current application URL
    },
    cache: {
        cacheLocation: 'localStorage', // Enable persistent storage
        storeAuthStateInCookie: false // Set to true for IE11 support
    }
};

// Request configuration for API access
const loginRequest = {
    scopes: ['openid', 'profile', 'email']
};

// Will be populated from server config
let apiRequest = {
    scopes: []
};

// Initialize MSAL instance
let msalInstance;
let currentUser = null;
let configLoaded = false;

// Ensure MSAL is available
function ensureMSAL() {
    return new Promise((resolve, reject) => {
        // Check if MSAL is already available
        if (typeof msal !== 'undefined') {
            resolve();
            return;
        }
        
        // If not available, wait for it to load
        console.log('Waiting for MSAL library to load...');
        let checkCount = 0;
        const maxChecks = 20; // Maximum number of checks (10 seconds)
        
        const checkMSAL = () => {
            if (typeof msal !== 'undefined') {
                console.log('MSAL library loaded successfully');
                resolve();
                return;
            }
            
            checkCount++;
            if (checkCount >= maxChecks) {
                reject(new Error('MSAL library failed to load after multiple attempts'));
                return;
            }
            
            // Check again in 500ms
            setTimeout(checkMSAL, 500);
        };
        
        // Start checking
        checkMSAL();
    });
}

/**
 * Fetch authentication configuration from the server
 */
async function fetchAuthConfig() {
    try {
        const response = await fetch('/api/config/auth');
        if (!response.ok) {
            throw new Error(`Failed to fetch auth config: ${response.status}`);
        }
        
        const config = await response.json();
        console.log('Auth config loaded from server');
        
        // Update MSAL configuration with values from server
        msalConfig.auth.clientId = config.clientId;
        msalConfig.auth.authority = config.authority;
        
        // Update API request scopes - force clear any cached scopes
        apiRequest.scopes = [config.apiScope];
        
        // Clear only MSAL-specific tokens to avoid interfering with login flow
        const msalKeys = Object.keys(localStorage).filter(key => 
            key.includes('msal') && (key.includes('accesstoken') || key.includes('idtoken'))
        );
        msalKeys.forEach(key => localStorage.removeItem(key));
        
        console.log('Cleared MSAL token cache:', msalKeys);
        
        configLoaded = true;
        return true;
    } catch (error) {
        console.error('Failed to load auth configuration:', error);
        return false;
    }
}

/**
 * Initialize the authentication system
 */
async function initializeAuth() {
    try {
        // Only clear problematic cached tokens, not all MSAL data
        const problematicKeys = Object.keys(localStorage).filter(key => 
            key.includes('msal') && key.includes('api://d4c452c4-5324-40ff-b43b-25f3daa2a45c')
        );
        problematicKeys.forEach(key => localStorage.removeItem(key));
        console.log('Cleared problematic cached tokens:', problematicKeys);
        
        // First fetch configuration from the server
        const configSuccess = await fetchAuthConfig();
        if (!configSuccess) {
            console.warn('Failed to load auth configuration from server');
            showConfigurationError();
            return false;
        }
        
        // Check if MSAL configuration is set
        if (!msalConfig.auth.clientId || !msalConfig.auth.authority) {
            console.warn('MSAL configuration not set. Authentication disabled.');
            showConfigurationError();
            return false;
        }

        // Ensure MSAL is loaded before proceeding
        try {
            await ensureMSAL();
        } catch (error) {
            console.error('Failed to load MSAL library:', error);
            showConfigurationError('Microsoft Authentication Library could not be loaded. Please refresh the page or try again later.');
            return false;
        }

        // Initialize MSAL
        msalInstance = new msal.PublicClientApplication(msalConfig);
        await msalInstance.initialize();

        // Handle redirect response
        const response = await msalInstance.handleRedirectPromise();
        if (response) {
            console.log('Login successful:', response);
            currentUser = response.account;
            updateUIForAuthenticatedUser();
            return true;
        }

        // Try silent authentication first
        const accounts = msalInstance.getAllAccounts();
        if (accounts.length > 0) {
            currentUser = accounts[0];
            msalInstance.setActiveAccount(currentUser);
            
            try {
                const token = await acquireTokenSilently();
                if (token) {
                    updateUIForAuthenticatedUser();
                    return true;
                }
            } catch (error) {
                console.log('Silent authentication failed:', error);
                // Fall through to show login UI
            }
        }

        // Show login UI if no valid session
        showLoginUI();
        return false;

    } catch (error) {
        console.error('Authentication initialization failed:', error);
        showAuthError('Authentication system failed to initialize');
        return false;
    }
}

/**
 * Perform interactive login
 */
async function login() {
    try {
        // In MSAL.js v3, we don't need to check interaction status the same way
        // Just proceed with login redirect
        
        showLoadingState('Signing in...');
        await msalInstance.loginRedirect(loginRequest);
        // Redirect will happen, so this code won't execute
    } catch (error) {
        console.error('Login failed:', error);
        showAuthError('Login failed. Please try again.');
        hideLoadingState();
    }
}

/**
 * Perform logout
 */
async function logout() {
    try {
        await msalInstance.logoutRedirect({
            postLogoutRedirectUri: window.location.origin
        });
    } catch (error) {
        console.error('Logout failed:', error);
        showAuthError('Logout failed. Please try again.');
    }
}

/**
 * Acquire access token silently
 */
async function acquireTokenSilently() {
    if (!currentUser) {
        throw new Error('No user account available');
    }

    const request = {
        ...apiRequest,
        account: currentUser
    };

    try {
        const response = await msalInstance.acquireTokenSilent(request);
        return response.accessToken;
    } catch (error) {
        console.log('Silent token acquisition failed:', error);
        
        // If silent acquisition fails, try interactive
        if (error instanceof msal.InteractionRequiredAuthError) {
            console.log('Interaction required, initiating redirect...');
            // For redirect flow, we don't get a return value immediately
            await msalInstance.acquireTokenRedirect(request);
            return null; // Will be handled on redirect return
        }
        throw error;
    }
}

/**
 * Get access token for API calls
 */
async function getAccessToken() {
    try {
        // Check if user is still authenticated
        if (!currentUser || !msalInstance) {
            console.log('No authenticated user or MSAL instance available');
            return null;
        }
        
        // Check if config is loaded
        if (!configLoaded) {
            console.log('Auth config not loaded yet');
            return null;
        }
        
        const token = await acquireTokenSilently();
        if (!token) {
            console.log('No token received from silent acquisition');
            return null;
        }
        
        return token;
    } catch (error) {
        console.error('Failed to get access token:', error);
        
        // Don't show error UI immediately - let the calling code handle it
        return null;
    }
}

/**
 * Check if user is authenticated
 */
function isAuthenticated() {
    return currentUser !== null && msalInstance !== null;
}

/**
 * Get current user information
 */
function getCurrentUser() {
    return currentUser;
}

/**
 * Update UI for authenticated user
 */
function updateUIForAuthenticatedUser() {
    hideElement('login-container');
    showElement('app-container');
    
    if (currentUser) {
        const userNameElement = document.getElementById('user-name');
        if (userNameElement) {
            userNameElement.textContent = currentUser.name || currentUser.username || 'User';
        }
    }
    
    hideLoadingState();
}

/**
 * Show login UI
 */
function showLoginUI() {
    showElement('login-container');
    hideElement('app-container');
    hideLoadingState();
}

/**
 * Show configuration error
 */
function showConfigurationError() {
    const errorHtml = `
        <div class="auth-error">
            <h3>Authentication Configuration Required</h3>
            <p>Please configure Entra ID settings in the application configuration.</p>
        </div>
    `;
    document.body.innerHTML = errorHtml;
}

/**
 * Show authentication error
 */
function showAuthError(message) {
    const errorElement = document.getElementById('auth-error');
    if (errorElement) {
        errorElement.textContent = message;
        showElement('auth-error');
        setTimeout(() => hideElement('auth-error'), 5000);
    }
}

/**
 * Show loading state
 */
function showLoadingState(message = 'Loading...') {
    const loadingElement = document.getElementById('loading-message');
    if (loadingElement) {
        loadingElement.textContent = message;
        showElement('loading-container');
    }
}

/**
 * Hide loading state
 */
function hideLoadingState() {
    hideElement('loading-container');
}

/**
 * Utility functions for showing/hiding elements
 */
function showElement(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.style.display = 'block';
    }
}

function hideElement(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.style.display = 'none';
    }
}

// Export functions for use in other modules
window.authModule = {
    initializeAuth,
    login,
    logout,
    getAccessToken,
    isAuthenticated,
    getCurrentUser
};
