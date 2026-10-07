/**
 * Prepares a photo in the browser before it is uploaded: scaled down so the longest side is at
 * most `maxSide` (phone photos are 4000px and several MB; a reviewer or a profile picture needs
 * far less), optionally cropped to a centred square, and re-encoded as JPEG. Re-encoding also
 * drops the camera's metadata, including where the photo was taken; the server strips it again
 * anyway.
 *
 * Falls back to the original file when the browser cannot decode it here (some HEIC photos);
 * the server then decides whether it is acceptable.
 */
export async function prepareImage(
  file: File,
  {
    maxSide,
    square = false,
    quality = 0.88,
  }: { maxSide: number; square?: boolean; quality?: number },
): Promise<File> {
  if (typeof createImageBitmap !== 'function' || typeof document === 'undefined') {
    return file;
  }

  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file);
  } catch {
    return file;
  }

  const side = Math.min(bitmap.width, bitmap.height);
  const sourceWidth = square ? side : bitmap.width;
  const sourceHeight = square ? side : bitmap.height;
  const sourceX = square ? (bitmap.width - side) / 2 : 0;
  const sourceY = square ? (bitmap.height - side) / 2 : 0;
  const scale = Math.min(1, maxSide / Math.max(sourceWidth, sourceHeight));
  const width = Math.round(sourceWidth * scale);
  const height = Math.round(sourceHeight * scale);

  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d');
  if (!context) {
    bitmap.close();
    return file;
  }

  context.drawImage(bitmap, sourceX, sourceY, sourceWidth, sourceHeight, 0, 0, width, height);
  bitmap.close();

  const blob = await new Promise<Blob | null>((resolve) =>
    canvas.toBlob(resolve, 'image/jpeg', quality),
  );
  if (!blob) {
    return file;
  }

  const name = file.name.replace(/\.[^.]+$/, '') + '.jpg';
  return new File([blob], name, { type: 'image/jpeg' });
}

/** The image types the API accepts. */
export const acceptedImageTypes = 'image/jpeg,image/png,image/webp';
