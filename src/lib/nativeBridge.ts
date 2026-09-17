/**
 * FLOW Universal Native IPC Bridge
 * Enables zero-latency, portless, bidirectional communication between the React UI
 * and the native Windows .NET 9 Host when running inside Microsoft.Web.WebView2.
 *
 * When running in a standard browser (development mode), transparently indicates
 * that the native shell is not present, allowing fallback to HTTP/SSE endpoints.
 */

export interface NativeMessage {
  requestId?: string;
  action?: string;
  event?: string;
  payload?: any;
  error?: string;
  success?: boolean;
}

const isWebView2Environment = (): boolean => {
  return (
    typeof window !== 'undefined' &&
    typeof (window as any).chrome !== 'undefined' &&
    typeof (window as any).chrome?.webview !== 'undefined' &&
    typeof (window as any).chrome?.webview?.postMessage === 'function'
  );
};

let initialized = false;
let nextRequestId = 1;
const pendingRequests = new Map<
  string,
  { resolve: (val: any) => void; reject: (err: any) => void; timer: any }
>();
const eventListeners = new Map<string, Set<(data: any) => void>>();

function ensureInitialized() {
  if (initialized || !isWebView2Environment()) return;
  initialized = true;

  try {
    (window as any).chrome.webview.addEventListener('message', (event: any) => {
      const data: NativeMessage = event.data;
      if (!data) return;

      // Handle RPC response
      if (data.requestId && pendingRequests.has(data.requestId)) {
        const pending = pendingRequests.get(data.requestId)!;
        pendingRequests.delete(data.requestId);
        clearTimeout(pending.timer);

        if (data.error) {
          pending.reject(new Error(data.error));
        } else {
          pending.resolve(data.payload !== undefined ? data.payload : data);
        }
        return;
      }

      // Handle broadcast native events
      if (data.event && eventListeners.has(data.event)) {
        const listeners = eventListeners.get(data.event)!;
        listeners.forEach((callback) => {
          try {
            callback(data.payload);
          } catch (err) {
            console.error(`[FLOW Native Bridge] Error in event listener for ${data.event}:`, err);
          }
        });
      }
    });
  } catch (err) {
    console.warn('[FLOW Native Bridge] Could not attach WebView2 message listener:', err);
  }
}

/**
 * Returns true if the React application is running embedded inside the FLOW .NET Host WebView2 window.
 */
export function isNativeShell(): boolean {
  return isWebView2Environment();
}

/**
 * Sends an asynchronous RPC request to the native .NET Host and awaits the response.
 */
export function sendNativeRequest<T = any>(
  action: string,
  payload: any = null,
  timeoutMs = 10000
): Promise<T> {
  ensureInitialized();

  if (!isWebView2Environment()) {
    return Promise.reject(new Error('WebView2 native host bridge is not available in standalone browser mode.'));
  }

  return new Promise<T>((resolve, reject) => {
    const requestId = `req_${Date.now()}_${nextRequestId++}`;
    const timer = setTimeout(() => {
      if (pendingRequests.has(requestId)) {
        pendingRequests.delete(requestId);
        reject(new Error(`Native request "${action}" timed out after ${timeoutMs}ms.`));
      }
    }, timeoutMs);

    pendingRequests.set(requestId, { resolve, reject, timer });

    try {
      (window as any).chrome.webview.postMessage({
        requestId,
        action,
        payload,
      });
    } catch (err) {
      pendingRequests.delete(requestId);
      clearTimeout(timer);
      reject(err);
    }
  });
}

/**
 * Subscribes to a broadcast event emitted by the native .NET Host.
 * Returns an unsubscription function.
 */
export function onNativeEvent<T = any>(
  eventName: string,
  callback: (data: T) => void
): () => void {
  ensureInitialized();

  if (!eventListeners.has(eventName)) {
    eventListeners.set(eventName, new Set());
  }

  const listeners = eventListeners.get(eventName)!;
  listeners.add(callback);

  return () => {
    listeners.delete(callback);
    if (listeners.size === 0) {
      eventListeners.delete(eventName);
    }
  };
}
