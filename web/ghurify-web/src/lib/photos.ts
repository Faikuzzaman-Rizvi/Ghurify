import type { DestinationKind } from '@/features/trips/tripsApi';

/**
 * Bundled destination photos, from Wikimedia Commons. Each is in /public/photos in three cuts:
 * `-card` (720x940 portrait, cropped on the subject), `-1080` and `-1920` (landscape, for
 * banners and heroes). The licences require credit: the photo credits page lists every one
 * from `photoCredits` below, so a new photo is added here and nowhere else.
 */
export interface DestinationPhoto {
  slug: string;
  /** Average colour, shown while the photo loads. */
  color: string;
  author: string;
  license: string;
  licenseUrl: string;
  source: string;
}

const photos: Record<string, DestinationPhoto> = {
  sajek: {
    slug: 'sajek',
    color: '#3b6775',
    author: 'G.B. G.Son',
    license: 'CC BY-SA 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0',
    source: 'https://commons.wikimedia.org/wiki/File:Sajek_Valley_20161205.jpg',
  },
  bandarban: {
    slug: 'bandarban',
    color: '#757a6f',
    author: 'Tareq Uddin Ahmed',
    license: 'CC BY 2.0',
    licenseUrl: 'https://creativecommons.org/licenses/by/2.0',
    source: 'https://commons.wikimedia.org/wiki/File:Bandarban_(02).jpg',
  },
  'coxs-bazar': {
    slug: 'coxs-bazar',
    color: '#959ba3',
    author: 'Fahimabrar420',
    license: 'CC BY-SA 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0',
    source: 'https://commons.wikimedia.org/wiki/File:Sea_Beach_View.jpg',
  },
  'saint-martins': {
    slug: 'saint-martins',
    color: '#7798ac',
    author: 'Tanvir Rahat',
    license: 'CC BY-SA 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0',
    source:
      'https://commons.wikimedia.org/wiki/File:Scenic_view_of_Saint_Martin_Island,_Bangladesh_2.jpg',
  },
  sylhet: {
    slug: 'sylhet',
    color: '#4e5b28',
    author: 'Sumon Mallick',
    license: 'CC BY-SA 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0',
    source: 'https://commons.wikimedia.org/wiki/File:Ratargul_Swamp_Forest,_Sylhet..jpg',
  },
  sreemangal: {
    slug: 'sreemangal',
    color: '#7e9a68',
    author: 'Mar11',
    license: 'CC BY-SA 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0',
    source: 'https://commons.wikimedia.org/wiki/File:Sreemangal_tea_garden_2017-08-20.jpg',
  },
  'tanguar-haor': {
    slug: 'tanguar-haor',
    color: '#8ca5bc',
    author: 'Ahnaf Tahmid Manan',
    license: 'CC BY-SA 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0',
    source: 'https://commons.wikimedia.org/wiki/File:Tanguar_Haor_Houseboat.jpg',
  },
  sundarbans: {
    slug: 'sundarbans',
    color: '#8e7c56',
    author: 'Shihabur Rahman',
    license: 'CC BY-SA 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0',
    source: 'https://commons.wikimedia.org/wiki/File:Save_the_sundarbans_20.jpg',
  },
  kuakata: {
    slug: 'kuakata',
    color: '#835032',
    author: 'Yahya',
    license: 'CC BY-SA 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0',
    source: 'https://commons.wikimedia.org/wiki/File:Sunsets_at_Kuakata_Sea_Beach.jpg',
  },
  rangamati: {
    slug: 'rangamati',
    color: '#5e6f70',
    author: 'Shakhawat Hossen Shafat',
    license: 'CC BY-SA 4.0',
    licenseUrl: 'https://creativecommons.org/licenses/by-sa/4.0',
    source: 'https://commons.wikimedia.org/wiki/File:Hanging_Bridge,_Kaptai_Lake.jpg',
  },
};

/** A destination added later, before it has its own photo, borrows one of the same kind. */
const byKind: Record<DestinationKind, string> = {
  Hills: 'bandarban',
  Beach: 'coxs-bazar',
  Island: 'saint-martins',
  Forest: 'sundarbans',
  Wetland: 'tanguar-haor',
  TeaGarden: 'sreemangal',
  Lake: 'rangamati',
  River: 'sylhet',
};

export function photoFor(slug: string, kind: DestinationKind): DestinationPhoto {
  return photos[slug] ?? photos[byKind[kind]]!;
}

/** Whether the photo is the destination's own, rather than one borrowed from its kind. */
export function hasOwnPhoto(slug: string): boolean {
  return slug in photos;
}

export type PhotoCut = 'card' | 'wide';

/** `src` and `srcSet` for one cut of a photo. */
export function photoSources(photo: DestinationPhoto, cut: PhotoCut) {
  const base = `/photos/${photo.slug}`;

  return cut === 'card'
    ? { src: `${base}-card.webp`, srcSet: undefined }
    : { src: `${base}-1080.webp`, srcSet: `${base}-1080.webp 1080w, ${base}-1920.webp 1920w` };
}

export const photoCredits: readonly DestinationPhoto[] = Object.values(photos);
