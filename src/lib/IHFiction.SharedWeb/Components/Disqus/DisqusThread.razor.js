export function loadDisqus(pageUrl, pageIdentifier, shortName) {
    const disqus_config = () => {
        this.page.url = pageUrl;
        this.page.identifier = pageIdentifier;
    };

    if (window.DISQUS) {
        DISQUS.reset({
            reload: true,
            config: disqus_config
        });
    } else {
        const d = document, s = d.createElement('script');
        s.src = `https://${shortName}.disqus.com/embed.js`;
        s.setAttribute('data-timestamp', +new Date());
        (d.head || d.body).appendChild(s);
    }
}