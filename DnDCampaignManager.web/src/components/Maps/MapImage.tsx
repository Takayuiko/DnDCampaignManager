import { useEffect, useRef, useState } from "react";
import { getMapImage, getMapThumbnail, type CampaignMap, type MapLocation } from "../../api/mapsApi";

export default function MapImage({ campaignId, map, file, locations, onPin, thumbnail = false }: {
    campaignId: number; map: CampaignMap | null; file?: File | null; locations: MapLocation[];
    onPin?: (x: number, y: number) => void; thumbnail?: boolean;
}) {
    const [url, setUrl] = useState("");
    const [error, setError] = useState("");
    const container = useRef<HTMLDivElement>(null);
    useEffect(() => {
        let active = true;
        let objectUrl = "";
        let started = false;
        const controller = new AbortController();
        const load = () => {
            if (started || !active) return;
            started = true;
            void Promise.resolve().then(async () => {
                if (!active) return;
                setUrl(""); setError("");
                const getImage = thumbnail ? getMapThumbnail : getMapImage;
                const blob = file ?? (map ? (await getImage(campaignId, map.id, controller.signal)).data : null);
                if (!active || !blob) return;
                objectUrl = URL.createObjectURL(blob); setUrl(objectUrl);
            }).catch(() => { if (active) setError("Unable to load map image. Reload to try again."); });
        };
        let observer: IntersectionObserver | undefined;
        if (thumbnail && container.current && typeof IntersectionObserver !== "undefined") {
            observer = new IntersectionObserver(entries => {
                if (entries.some(entry => entry.isIntersecting)) {
                    observer?.disconnect();
                    load();
                }
            });
            observer.observe(container.current);
        } else load();
        return () => {
            active = false;
            observer?.disconnect();
            controller.abort();
            if (objectUrl) URL.revokeObjectURL(objectUrl);
        };
    }, [campaignId, map, file, thumbnail]);
    return <div ref={container} className={thumbnail ? "relative flex h-32 w-full items-center justify-center overflow-hidden rounded-lg bg-stone-100" : "relative inline-block max-w-full"} onClick={event => {
        if (!onPin || !url) return;
        const bounds = event.currentTarget.getBoundingClientRect();
        onPin(Math.max(0, Math.min(100, (event.clientX - bounds.left) / bounds.width * 100)),
            Math.max(0, Math.min(100, (event.clientY - bounds.top) / bounds.height * 100)));
    }}>
        {error ? <p role="alert">{error}</p> : !url ?
            <p>{map ? "Loading image…" : "Choose an image to place location pins."}</p> : <>
                <img src={url} alt={map?.title ?? "Map draft"} className={thumbnail ? "h-full w-full object-contain" : "block max-h-[650px] max-w-full"} onError={() => setError("This image could not be displayed. Choose another image.")} />
                {!thumbnail && locations.map((location, i) => <span key={i} title={location.name || `Location ${i + 1}`}
                    style={{ left: `${location.x}%`, top: `${location.y}%`, transform: "translate(-50%, -50%)" }}
                    className="pointer-events-none absolute flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-red-800 text-sm font-bold text-white shadow">{i + 1}</span>)}
            </>}
    </div>;
}
