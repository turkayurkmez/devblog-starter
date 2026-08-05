import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PostService, PostDetail } from '../../services/post.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-post-detail',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './post-detail.component.html'
})
export class PostDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private cdr = inject(ChangeDetectorRef);
  private postService = inject(PostService);
  private authService = inject(AuthService);

  post: PostDetail | null = null;
  commentAuthor = '';
  commentBody = '';
  submitted = false;

  ngOnInit() {
    const slug = this.route.snapshot.paramMap.get('slug')!;
    this.postService.getPost(slug).subscribe(p => {
      this.post = p;
      this.cdr.detectChanges(); //bu satır, değişiklikleri algılamak ve bileşeni güncellemek için ChangeDetectorRef kullanır

    } );
  }

  get isLoggedIn(): boolean {
    return this.authService.isLoggedIn();
  }

  toggleLike() {
    if (!this.post || !this.isLoggedIn) return;
    this.postService.toggleLike(this.post.slug).subscribe(result => {
      if (!this.post) return;
      this.post.likeCount = result.likeCount;
      this.post.isLikedByCurrentUser = result.isLikedByCurrentUser;
      this.cdr.detectChanges();
    });
  }

  submitComment() {
    if (!this.post) return;
    this.postService
      .addComment(this.post.slug, { authorName: this.commentAuthor, body: this.commentBody })
      .subscribe(() => {
        this.submitted = true;
        this.commentAuthor = '';
        this.commentBody = '';
        const slug = this.route.snapshot.paramMap.get('slug')!;
        this.postService.getPost(slug).subscribe(p => (this.post = p));
      });
  }
}
