export function loadDisqus(pageUrl, pageIdentifier, shortName, nonce) {
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
        if (nonce) {
            s.nonce = nonce;
        }
        s.setAttribute('data-timestamp', +new Date());
        (d.head || d.body).appendChild(s);
    }
}
