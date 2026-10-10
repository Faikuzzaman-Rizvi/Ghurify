import { ApiError, apiDelete, apiGet, apiPost, apiPut } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type PostView = components['schemas']['PostView'];
export type PostPage = components['schemas']['PostPage'];
export type CommentView = components['schemas']['CommentView'];
export type PublicProfile = components['schemas']['PublicProfile'];
export type ReviewView = components['schemas']['ReviewView'];
export type Reviewable = components['schemas']['Reviewable'];
export type UploadLink = components['schemas']['UploadLink'];
export type CreatePostCommand = components['schemas']['CreatePostCommand'];
export type EditPostCommand = components['schemas']['EditPostCommand'];
export type AddReviewCommand = components['schemas']['AddReviewCommand'];

/** Stories, people and reviews. */
export const feedApi = {
  feed: (before?: number, signal?: AbortSignal) =>
    apiGet<PostPage>(`/api/v1/feed${before ? `?before=${before}` : ''}`, signal ? { signal } : {}),

  userPosts: (userId: number, signal?: AbortSignal) =>
    apiGet<PostPage>(`/api/v1/users/${userId}/posts`, signal ? { signal } : {}),

  createPost: (command: CreatePostCommand) => apiPost<{ id: number }>('/api/v1/posts', command),

  deletePost: (postId: number) => apiDelete<void>(`/api/v1/posts/${postId}`),

  /** The author's change: new text and place, and the ids of the photos that stay. */
  editPost: (postId: number, command: EditPostCommand) =>
    apiPut<void>(`/api/v1/posts/${postId}`, command),

  like: (postId: number) => apiPost<void>(`/api/v1/posts/${postId}/likes`),

  unlike: (postId: number) => apiDelete<void>(`/api/v1/posts/${postId}/likes`),

  comments: (postId: number, signal?: AbortSignal) =>
    apiGet<CommentView[]>(`/api/v1/posts/${postId}/comments`, signal ? { signal } : {}),

  addComment: (postId: number, body: string) =>
    apiPost<CommentView>(`/api/v1/posts/${postId}/comments`, { body }),

  profile: (userId: number, signal?: AbortSignal) =>
    apiGet<PublicProfile>(`/api/v1/users/${userId}`, signal ? { signal } : {}),

  follow: (userId: number) => apiPost<void>(`/api/v1/users/${userId}/follow`),

  unfollow: (userId: number) => apiDelete<void>(`/api/v1/users/${userId}/follow`),

  uploadLink: (contentType: string, sizeBytes: number) =>
    apiPost<UploadLink>('/api/v1/media/upload-url', { contentType, sizeBytes }),

  completeUpload: (mediaId: number) => apiPost<void>(`/api/v1/media/${mediaId}/complete`),

  reviewable: (tripId: number, signal?: AbortSignal) =>
    apiGet<Reviewable[]>(`/api/v1/trips/${tripId}/reviewable`, signal ? { signal } : {}),

  addReview: (tripId: number, command: AddReviewCommand) =>
    apiPost<{ id: number }>(`/api/v1/trips/${tripId}/reviews`, command),
};

/**
 * Why an upload straight to storage failed, as a code the screen has a translation for. The
 * status tells them apart: storage answered and refused (the link ran out, the file is bigger
 * than the link allows), or it never answered at all. Guessing "check your connection" at a
 * 403 sends people to look at the wrong thing.
 */
function uploadFailure(status: number): ApiError {
  const code =
    status === 0
      ? 'upload_offline'
      : status === 403 || status === 401
        ? 'upload_link_expired'
        : status === 413
          ? 'media_too_large'
          : 'upload_rejected';
  return new ApiError(`Upload failed (${status || 'no answer'})`, status, undefined, code);
}

/**
 * Uploads a file straight to storage with the link the API gave, reporting progress. A plain XHR,
 * because fetch cannot report upload progress. A failure is an ApiError carrying why, so the
 * screen can name the reason rather than say something vaguer.
 */
export function uploadToStorage(
  url: string,
  file: File,
  onProgress: (fraction: number) => void,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const request = new XMLHttpRequest();
    request.open('PUT', url);
    request.setRequestHeader('x-ms-blob-type', 'BlockBlob');
    request.setRequestHeader('Content-Type', file.type);
    request.upload.onprogress = (event) => {
      if (event.lengthComputable) onProgress(event.loaded / event.total);
    };
    request.onload = () =>
      request.status >= 200 && request.status < 300
        ? resolve()
        : reject(uploadFailure(request.status));
    // No answer at all: offline, or storage cannot be reached from this network.
    request.onerror = () => reject(uploadFailure(0));
    request.ontimeout = () => reject(uploadFailure(0));
    request.send(file);
  });
}
