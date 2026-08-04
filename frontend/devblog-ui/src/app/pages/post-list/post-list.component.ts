import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { PostService, PostSummary } from '../../services/post.service';

@Component({
  selector: 'app-post-list',
  standalone: true,
  imports: [RouterLink, CommonModule],
  templateUrl: './post-list.component.html'
})
export class PostListComponent implements OnInit {
  private postService = inject(PostService);
    private cdr = inject(ChangeDetectorRef);
  posts: PostSummary[] = [];
  page = 1;
  totalPages = 1;

  ngOnInit() {
    this.loadPosts();
  }

  loadPosts() {
    this.postService.getPosts(this.page).subscribe((result) => {
      this.posts = result.items;
      this.totalPages = result.totalPages;
      this.cdr.detectChanges(); //bu satır, değişiklikleri algılamak ve bileşeni güncellemek için ChangeDetectorRef kullanır
    });
  }

  goToPage(page: number) {
    if (page < 1 || page > this.totalPages) return;
    this.page = page;
    this.loadPosts();
  }
}
