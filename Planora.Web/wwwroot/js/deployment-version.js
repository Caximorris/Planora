window.planoraVersionMonitor = (() => {
    let dotNetReference = null;

    const onVisibilityChange = () => {
        if (document.visibilityState === "visible") {
            dotNetReference?.invokeMethodAsync("CheckForDeploymentUpdate");
        }
    };

    const unregister = () => {
        document.removeEventListener("visibilitychange", onVisibilityChange);
        dotNetReference = null;
    };

    return {
        register(reference) {
            unregister();
            dotNetReference = reference;
            document.addEventListener("visibilitychange", onVisibilityChange);
        },
        unregister
    };
})();
