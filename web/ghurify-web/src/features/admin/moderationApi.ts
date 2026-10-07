import { apiGet, apiPost } from '@/api/client';
import type { PostPage } from '@/features/feed/feedApi';

/** Story moderation from the admin portal: every story, and removing one with a reason. */
export const moderationApi = {
  posts: (authorId: number | null, before: number | null, signal?: AbortSignal) => {
    const query = new URLSearchParams({
      ...(authorId ? { authorId: String(authorId) } : {}),
      ...(before ? { before: String(before) } : {}),
    }).toString();
    return apiGet<PostPage>(
      `/api/v1/admin/posts${query ? `?${query}` : ''}`,
      signal ? { signal } : {},
    );
  },

  removePost: (postId: number, reason: string) =>
    apiPost<void>(`/api/v1/admin/posts/${postId}/remove`, { reason }),
};
