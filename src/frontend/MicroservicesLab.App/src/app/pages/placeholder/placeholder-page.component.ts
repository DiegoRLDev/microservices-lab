import { Component, computed, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LabHealthService } from '../../core/api/lab-health.service';

interface PageData {
  title: string;
  description: string;
  phase: string;
}

@Component({
  selector: 'mlab-placeholder-page',
  standalone: true,
  templateUrl: './placeholder-page.component.html',
  styleUrl: './placeholder-page.component.css'
})
export class PlaceholderPageComponent {
  private readonly route = inject(ActivatedRoute);
  protected readonly health = inject(LabHealthService);

  protected readonly page = computed<PageData>(() => {
    const data = this.route.snapshot.data;

    return {
      title: String(data['title'] ?? 'MicroservicesLab'),
      description: String(data['description'] ?? 'Laboratorio disponivel.'),
      phase: String(data['phase'] ?? 'Parte III')
    };
  });
}
