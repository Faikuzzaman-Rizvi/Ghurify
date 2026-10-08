import { latToY, lonToX } from '@/components/ui/mapProjection';
import { countryOutline, districtShapes, divisionLines } from './districtShapes';
import { districtBounds, layoutLabels, mapBox, pinColours, pinPath } from './districts';
import type { DistrictPin } from './DistrictMap';
import type { MapTheme } from './mapThemes';

/** The poster's words, already in the reader's language. */
export interface PosterText {
  eyebrow: string;
  title: string;
  count: string;
  total: string;
  explored: string;
  counts: string;
  brand: string;
  tagline: string;
  /** District slug -> its name, for the visited ones. */
  districtName: (slug: string) => string;
}

export interface PosterContent {
  theme: MapTheme;
  text: PosterText;
  visited: ReadonlySet<string>;
  /** Share of the 64 districts, 0 to 1. */
  progress: number;
  pins: readonly DistrictPin[];
  showLabels: boolean;
  /** A link to their picture; left off the poster when it cannot be loaded. */
  photoUrl?: string | null;
}

export type PosterFormat = 'png' | 'jpg' | 'pdf';

const width = 1200;
const height = 1600;
const margin = 76;

function loadImage(src: string, crossOrigin: boolean): Promise<HTMLImageElement | null> {
  return new Promise((resolve) => {
    const image = new Image();
    if (crossOrigin) image.crossOrigin = 'anonymous';
    image.onload = () => resolve(image);
    image.onerror = () => resolve(null);
    image.src = src;
  });
}

/** The app's heading and body fonts, as the stylesheet defines them. */
function fonts() {
  const style = getComputedStyle(document.documentElement);
  return {
    display:
      style.getPropertyValue('--font-display').trim() || "'Poppins', 'Baloo Da 2', sans-serif",
    body: style.getPropertyValue('--font-sans').trim() || "'Barlow', 'Hind Siliguri', sans-serif",
  };
}

/** Shortens text with an ellipsis until it fits. */
function fit(context: CanvasRenderingContext2D, text: string, room: number): string {
  if (context.measureText(text).width <= room) return text;
  let cut = text;
  while (cut.length > 1 && context.measureText(`${cut}…`).width > room) cut = cut.slice(0, -1);
  return `${cut.trimEnd()}…`;
}

function roundedRect(
  context: CanvasRenderingContext2D,
  x: number,
  y: number,
  w: number,
  h: number,
  r: number,
) {
  context.beginPath();
  context.roundRect(x, y, w, h, r);
}

/** Draws the poster, the same layout as TravelPoster on the page, at 1200 × 1600. */
export async function drawPoster(content: PosterContent): Promise<HTMLCanvasElement> {
  const { theme, text } = content;
  const font = fonts();
  // Canvas text only uses fonts already loaded: ask for every weight and script it needs.
  const sample = `${text.title}${text.explored}${text.districtName('dhaka')}`;
  await Promise.all(
    [
      `800 64px ${font.display}`,
      `700 20px ${font.display}`,
      `600 24px ${font.body}`,
      `400 24px ${font.body}`,
    ].map((spec) => document.fonts.load(spec, sample).catch(() => [])),
  );

  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d');
  if (!context) throw new Error('canvas');

  context.fillStyle = theme.paper;
  context.fillRect(0, 0, width, height);

  // The count, top right: a big number and "/64".
  context.textBaseline = 'alphabetic';
  context.textAlign = 'right';
  context.font = `700 44px ${font.display}`;
  const totalWidth = context.measureText(`/${text.total}`).width;
  context.fillStyle = theme.muted;
  context.fillText(`/${text.total}`, width - margin, 212);
  context.font = `800 136px ${font.display}`;
  context.fillStyle = theme.accent;
  context.fillText(text.count, width - margin - totalWidth - 8, 212);
  const countLeft = width - margin - totalWidth - 8 - context.measureText(text.count).width;

  // Their picture, then whose map this is.
  let left = margin;
  const photo = content.photoUrl ? await loadImage(content.photoUrl, true) : null;
  if (photo) {
    context.save();
    context.beginPath();
    context.arc(margin + 52, 168, 52, 0, Math.PI * 2);
    context.clip();
    const side = Math.min(photo.naturalWidth, photo.naturalHeight);
    context.drawImage(
      photo,
      (photo.naturalWidth - side) / 2,
      (photo.naturalHeight - side) / 2,
      side,
      side,
      margin,
      116,
      104,
      104,
    );
    context.restore();
    context.lineWidth = 5;
    context.strokeStyle = theme.paper;
    context.beginPath();
    context.arc(margin + 52, 168, 54, 0, Math.PI * 2);
    context.stroke();
    left += 128;
  }
  context.textAlign = 'left';
  context.font = `600 22px ${font.body}`;
  context.fillStyle = theme.muted;
  context.fillText(
    fit(context, text.eyebrow.toLocaleUpperCase(), countLeft - left - 24),
    left,
    148,
  );
  // A long name first gets smaller, down to 40px, and only then shortened.
  const titleRoom = countLeft - left - 24;
  let titleSize = 64;
  context.font = `800 ${titleSize}px ${font.display}`;
  while (titleSize > 40 && context.measureText(text.title).width > titleRoom) {
    titleSize -= 2;
    context.font = `800 ${titleSize}px ${font.display}`;
  }
  context.fillStyle = theme.ink;
  context.fillText(fit(context, text.title, titleRoom), left, 214);

  // The map, as large as the space allows, centred.
  const top = 262;
  const room = { width: width - margin * 2, height: 1050 };
  const scale = Math.min(room.width / mapBox.width, room.height / mapBox.height);
  const offsetX = (width - mapBox.width * scale) / 2 - mapBox.x * scale;
  const offsetY = top + (room.height - mapBox.height * scale) / 2 - mapBox.y * scale;
  const px = 1 / scale;

  context.save();
  context.translate(offsetX, offsetY);
  context.scale(scale, scale);

  context.save();
  context.filter = 'blur(6px)';
  context.globalAlpha = theme.id === 'night' ? 0.5 : 0.12;
  context.translate(3, 6);
  context.fillStyle = theme.ink;
  context.fill(new Path2D(countryOutline), 'evenodd');
  context.restore();

  context.lineJoin = 'round';
  context.lineCap = 'round';
  for (const shape of districtShapes) {
    const path = new Path2D(shape.d);
    if (content.visited.has(shape.slug)) {
      const box = districtBounds(shape.slug);
      const fill = context.createLinearGradient(
        box.x,
        box.y,
        box.x + box.width * 0.4,
        box.y + box.height,
      );
      fill.addColorStop(0, theme.visited[0]);
      fill.addColorStop(1, theme.visited[1]);
      context.fillStyle = fill;
    } else {
      context.fillStyle = theme.land;
    }
    context.fill(path, 'evenodd');
    context.strokeStyle = theme.border;
    context.lineWidth = 0.7;
    context.stroke(path);
  }
  context.strokeStyle = theme.divisionLine;
  context.lineWidth = 1.3;
  context.stroke(new Path2D(divisionLines));

  const pin = new Path2D(pinPath);
  const pinSize = 13 * px;
  for (const item of content.pins) {
    context.save();
    context.translate(lonToX(item.longitude), latToY(item.latitude));
    context.save();
    context.scale(pinSize, pinSize);
    context.fillStyle = item.tone === 'upcoming' ? '#ffffff' : pinColours[item.tone];
    context.fill(pin);
    context.lineWidth = (item.tone === 'upcoming' ? 3.6 : 3) / 13;
    context.strokeStyle = item.tone === 'upcoming' ? pinColours.upcoming : '#ffffff';
    context.stroke(pin);
    context.restore();
    context.beginPath();
    context.arc(0, -pinSize * 1.8, pinSize * 0.38, 0, Math.PI * 2);
    context.fillStyle = item.tone === 'upcoming' ? pinColours.upcoming : '#ffffff';
    context.fill();
    context.restore();
  }

  // Names over the pins, as on the page.
  if (content.showLabels) {
    const size = 19 * px;
    context.font = `700 ${size}px ${font.display}`;
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.lineWidth = size * 0.32;
    const names = districtShapes
      .filter((shape) => content.visited.has(shape.slug))
      .map((shape) => ({ slug: shape.slug, text: text.districtName(shape.slug) }));
    for (const label of layoutLabels(names, size)) {
      context.strokeStyle = theme.labelHalo;
      context.strokeText(label.text, label.x, label.y);
      context.fillStyle = theme.label;
      context.fillText(label.text, label.x, label.y);
    }
  }
  context.restore();

  // How far along: the bar, the share, the counts.
  const barTop = top + room.height + 34;
  roundedRect(context, margin, barTop, width - margin * 2, 14, 7);
  context.fillStyle = theme.track;
  context.fill();
  if (content.progress > 0) {
    const fill = context.createLinearGradient(margin, 0, width - margin, 0);
    fill.addColorStop(0, theme.visited[0]);
    fill.addColorStop(1, theme.accent);
    roundedRect(
      context,
      margin,
      barTop,
      Math.max(14, (width - margin * 2) * content.progress),
      14,
      7,
    );
    context.fillStyle = fill;
    context.fill();
  }
  context.textBaseline = 'alphabetic';
  context.textAlign = 'left';
  context.font = `700 30px ${font.display}`;
  context.fillStyle = theme.ink;
  context.fillText(text.explored, margin, barTop + 62);
  context.textAlign = 'right';
  context.font = `400 24px ${font.body}`;
  context.fillStyle = theme.muted;
  context.fillText(text.counts, width - margin, barTop + 60);

  // Ghurify, at the foot.
  const footTop = height - 110;
  context.fillStyle = theme.track;
  context.fillRect(margin, footTop, width - margin * 2, 2);
  const logo = await loadImage('/favicon.svg', false);
  context.font = `700 28px ${font.display}`;
  const brandWidth = context.measureText(text.brand).width;
  context.font = `400 24px ${font.body}`;
  const tagline = ` · ${text.tagline}`;
  const taglineWidth = context.measureText(tagline).width;
  const logoSize = 44;
  let x = (width - (logoSize + 14 + brandWidth + taglineWidth)) / 2;
  if (logo) context.drawImage(logo, x, footTop + 32, logoSize, logoSize);
  x += logoSize + 14;
  context.textAlign = 'left';
  context.font = `700 28px ${font.display}`;
  context.fillStyle = theme.ink;
  context.fillText(text.brand, x, footTop + 64);
  context.font = `400 24px ${font.body}`;
  context.fillStyle = theme.muted;
  context.fillText(tagline, x + brandWidth, footTop + 64);

  return canvas;
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality?: number): Promise<Blob> {
  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('export'))), type, quality),
  );
}

/**
 * A one-page PDF holding a JPEG, written by hand: a PDF is a few numbered objects and a table of
 * their byte offsets. Page size in points, keeping the image's shape (600 × 800 for the poster).
 */
export function pdfFromJpeg(jpeg: Uint8Array, pixelWidth: number, pixelHeight: number): Blob {
  const pageWidth = 600;
  const pageHeight = Math.round((pageWidth * pixelHeight) / pixelWidth);
  const encoder = new TextEncoder();
  const chunks: Uint8Array[] = [];
  const offsets: number[] = [];
  let length = 0;
  const write = (part: string | Uint8Array) => {
    const bytes = typeof part === 'string' ? encoder.encode(part) : part;
    chunks.push(bytes);
    length += bytes.length;
  };
  const object = (body: string) => {
    offsets.push(length);
    write(`${offsets.length} 0 obj\n${body}\nendobj\n`);
  };

  const drawing = `q ${pageWidth} 0 0 ${pageHeight} 0 0 cm /Poster Do Q`;
  write('%PDF-1.4\n%âãÏÓ\n');
  object('<< /Type /Catalog /Pages 2 0 R >>');
  object('<< /Type /Pages /Kids [3 0 R] /Count 1 >>');
  object(
    `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${pageWidth} ${pageHeight}] ` +
      '/Resources << /XObject << /Poster 4 0 R >> >> /Contents 5 0 R >>',
  );
  offsets.push(length);
  write(
    `4 0 obj\n<< /Type /XObject /Subtype /Image /Width ${pixelWidth} /Height ${pixelHeight} ` +
      `/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length ${jpeg.length} >>\nstream\n`,
  );
  write(jpeg);
  write('\nendstream\nendobj\n');
  object(`<< /Length ${drawing.length} >>\nstream\n${drawing}\nendstream`);

  const table = length;
  write(`xref\n0 ${offsets.length + 1}\n0000000000 65535 f \n`);
  for (const offset of offsets) write(`${String(offset).padStart(10, '0')} 00000 n \n`);
  write(`trailer\n<< /Size ${offsets.length + 1} /Root 1 0 R >>\nstartxref\n${table}\n%%EOF\n`);

  return new Blob(chunks as BlobPart[], { type: 'application/pdf' });
}

/** Draws the poster and saves it in the chosen format. */
export async function downloadPoster(
  content: PosterContent,
  format: PosterFormat,
  fileName: string,
) {
  const canvas = await drawPoster(content);
  let blob: Blob;
  if (format === 'png') {
    blob = await toBlob(canvas, 'image/png');
  } else {
    const jpeg = await toBlob(canvas, 'image/jpeg', 0.92);
    blob =
      format === 'jpg'
        ? jpeg
        : pdfFromJpeg(new Uint8Array(await jpeg.arrayBuffer()), canvas.width, canvas.height);
  }

  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${fileName}.${format}`;
  document.body.append(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
