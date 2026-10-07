import { BangladeshArtwork } from './BangladeshMap';

/**
 * Behind the public site: the map of Bangladesh as quiet artwork, fixed on the right while the
 * page scrolls. A mask fades it out towards the middle of the screen, where text sits, so it is
 * present at the edge and gone by the content column. It only shows through on plain white
 * sections; photo bands, tinted sections and cards sit over it. Decorative, aria-hidden and
 * unclickable.
 */
export function SiteBackdrop() {
  return (
    <div aria-hidden="true" className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
      {/* A soft wash of colour where the map sits, so it has a ground to stand on. */}
      <div className="absolute -right-40 top-1/2 h-[70vh] w-[50vw] -translate-y-1/2 rounded-full bg-linear-to-bl from-sky/60 via-mist/40 to-transparent blur-3xl" />
      <BangladeshArtwork className="absolute -right-56 top-1/2 h-[118vh] max-h-320 w-auto -translate-y-1/2 opacity-45 mask-[linear-gradient(to_left,black_30%,rgb(0_0_0/0.55)_55%,transparent_88%)] sm:-right-40 lg:-right-24 xl:right-[2%]" />
    </div>
  );
}
