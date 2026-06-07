export const routerState = {
  routes: {},
  currentPath: ''
};

export function navigate(hashPath) {
  window.location.hash = hashPath;
}

export function registerRoute(hashPath, renderFunction) {
  routerState.routes[hashPath] = renderFunction;
}

export function handleRoute() {
  const hash = window.location.hash || '#/';
  
  const questionMarkIndex = hash.indexOf('?');
  const path = questionMarkIndex !== -1 ? hash.substring(0, questionMarkIndex) : hash;
  const queryString = questionMarkIndex !== -1 ? hash.substring(questionMarkIndex + 1) : '';
  
  console.log("[Router] handleRoute hash:", hash, "path:", path, "queryString:", queryString);
  routerState.currentPath = hash;
  
  let matched = false;
  
  for (const route in routerState.routes) {
    // Dynamic matching regex for secret viewing link: #/secret/secretId:key
    if (route === '#/secret' && path.startsWith('#/secret/')) {
      const paramStr = path.substring(9);
      const colonIndex = paramStr.indexOf(':');
      if (colonIndex !== -1) {
        const id = paramStr.substring(0, colonIndex);
        const key = paramStr.substring(colonIndex + 1);
        routerState.routes[route]({ id, key });
        matched = true;
        break;
      }
    }
    
    if (route === path) {
      const queryParams = {};
      if (queryString) {
        queryString.split('&').forEach(param => {
          const [k, v] = param.split('=');
          if (k) {
            queryParams[decodeURIComponent(k)] = decodeURIComponent(v || '');
          }
        });
      }
      routerState.routes[route](queryParams);
      matched = true;
      break;
    }
  }

  if (!matched) {
    console.warn("[Router] No route matched for path:", path, "Redirecting to root.");
    navigate('/');
  }
}
