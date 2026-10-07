import { apiDelete, apiGet, apiPost } from '@/api/client';
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
export type AddReviewCommand = components['schemas']['AddReviewCommand'];

/** Stories, people and reviews. */
export const feedApi = {
  feed: (before?: number, signal?: AbortSignal) =>
    apiGet<PostPage>(`/api/v1/feed${before ? `?before=${before}` : ''}`, signal ? { signal } : {}),

  userPosts: (userId: number, signal?: AbortSignal) =>
    apiGet<PostPage>(`/api/v1/users/${userId}/posts`, signal ? { signal } : {}),

  createPost: (command: CreatePostCommand) => apiPost<{ id: number }>('/api/v1/posts', command),

  deletePost: (postId: number) => apiDelete<void>(`/api/v1/posts/${postId}`),

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
 * Uploads a file straight to storage with the link the API gave, reporting progress. A plain XHR,
 * because fetch cannot report upload progress.
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
        : reject(new Error(`Upload failed (${request.status})`));
    request.onerror = () => reject(new Error('Upload failed'));
    request.send(file);
  });
}
