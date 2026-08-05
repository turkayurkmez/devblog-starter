import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

export interface PostSummary {
  id: number;
  title: string;
  slug: string;
  tags: string;
  publishedAt: string;
  readingInMinutes: number;
  author: string;
  likeCount: number;
  isLikedByCurrentUser: boolean;
}

export interface PostDetail extends PostSummary {
  content: string;
  comments: Comment[];
}

export interface LikeResult {
  likeCount: number;
  isLikedByCurrentUser: boolean;
}

export interface Comment {
  id: number;
  authorName: string;
  body: string;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

@Injectable({ providedIn: 'root' })
export class PostService {
  private http = inject(HttpClient);

  getPosts(page = 1, pageSize = 10) {
    return this.http.get<PagedResult<PostSummary>>(`${environment.apiUrl}/posts`, {
      params: { page, pageSize }
    });
  }

  getPost(slug: string) {
    return this.http.get<PostDetail>(`${environment.apiUrl}/posts/${slug}`);
  }

  createPost(data: { title: string; content: string; slug: string; tags: string }) {
    return this.http.post(`${environment.apiUrl}/posts`, data);
  }

  addComment(slug: string, data: { authorName: string; body: string }) {
    return this.http.post(`${environment.apiUrl}/posts/${slug}/comments`, data);
  }

  toggleLike(slug: string) {
    return this.http.post<LikeResult>(`${environment.apiUrl}/posts/${slug}/like`, {});
  }
}
