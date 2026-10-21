import { CurrencyPipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import {
  InventoryApiService,
  ProductAvailabilityResponse,
  ProductResponse
} from '../../core/api/inventory-api.service';

type LoadStatus = 'idle' | 'loading' | 'success' | 'error';

interface AvailabilityState {
  status: LoadStatus;
  result: ProductAvailabilityResponse | null;
  error: string | null;
}

@Component({
  selector: 'mlab-products-page',
  standalone: true,
  imports: [CurrencyPipe, ReactiveFormsModule],
  templateUrl: './products-page.component.html',
  styleUrl: './products-page.component.css'
})
export class ProductsPageComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(FormBuilder);
  private readonly inventoryApi = inject(InventoryApiService);

  protected readonly products = signal<ProductResponse[]>([]);
  protected readonly productsStatus = signal<LoadStatus>('idle');
  protected readonly productsError = signal<string | null>(null);
  protected readonly createStatus = signal<LoadStatus>('idle');
  protected readonly createMessage = signal<string | null>(null);
  protected readonly createError = signal<string | null>(null);
  protected readonly selectedProduct = signal<ProductResponse | null>(null);
  protected readonly detailsStatus = signal<LoadStatus>('idle');
  protected readonly detailsError = signal<string | null>(null);
  protected readonly availability = signal<AvailabilityState>({
    status: 'idle',
    result: null,
    error: null
  });

  protected readonly productForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    price: [0, [Validators.required, Validators.min(0)]],
    availableQuantity: [0, [Validators.required, Validators.min(0)]]
  });

  protected readonly availabilityForm = this.formBuilder.nonNullable.group({
    quantity: [1, [Validators.required, Validators.min(1)]]
  });

  protected readonly hasProducts = computed(() => this.products().length > 0);

  constructor() {
    this.loadProducts();
  }

  protected loadProducts() {
    this.productsStatus.set('loading');
    this.productsError.set(null);

    this.inventoryApi.listProducts()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (products) => {
          this.products.set(products);
          this.productsStatus.set('success');

          const selected = this.selectedProduct();
          if (selected) {
            const refreshed = products.find((product) => product.id === selected.id) ?? null;
            this.selectedProduct.set(refreshed);
          }
        },
        error: (error: HttpErrorResponse) => {
          this.productsStatus.set('error');
          this.productsError.set(this.toErrorMessage(error, 'Nao foi possivel carregar produtos.'));
        }
      });
  }

  protected createProduct() {
    this.productForm.markAllAsTouched();
    this.createMessage.set(null);
    this.createError.set(null);

    if (this.productForm.invalid) {
      this.createError.set('Revise os campos destacados antes de cadastrar.');
      return;
    }

    const request = this.productForm.getRawValue();
    this.createStatus.set('loading');

    this.inventoryApi.createProduct({
      name: request.name.trim(),
      price: request.price,
      availableQuantity: request.availableQuantity
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (created) => {
          this.createStatus.set('success');
          this.createMessage.set(`Produto "${created.name}" cadastrado com sucesso.`);
          this.productForm.reset({
            name: '',
            price: 0,
            availableQuantity: 0
          });
          this.loadProducts();
          this.selectProduct(created.id);
        },
        error: (error: HttpErrorResponse) => {
          this.createStatus.set('error');
          this.createError.set(this.toErrorMessage(error, 'Nao foi possivel cadastrar o produto.'));
        }
      });
  }

  protected selectProduct(id: string) {
    this.detailsStatus.set('loading');
    this.detailsError.set(null);
    this.availability.set({
      status: 'idle',
      result: null,
      error: null
    });

    this.inventoryApi.getProduct(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (product) => {
          this.selectedProduct.set(product);
          this.detailsStatus.set('success');
        },
        error: (error: HttpErrorResponse) => {
          this.selectedProduct.set(null);
          this.detailsStatus.set('error');
          this.detailsError.set(this.toErrorMessage(error, 'Nao foi possivel consultar o produto.'));
        }
      });
  }

  protected checkAvailability() {
    this.availabilityForm.markAllAsTouched();

    const product = this.selectedProduct();
    if (!product) {
      this.availability.set({
        status: 'error',
        result: null,
        error: 'Selecione um produto antes de consultar disponibilidade.'
      });
      return;
    }

    if (this.availabilityForm.invalid) {
      this.availability.set({
        status: 'error',
        result: null,
        error: 'Informe uma quantidade maior que zero.'
      });
      return;
    }

    this.availability.set({
      status: 'loading',
      result: null,
      error: null
    });

    this.inventoryApi.checkAvailability(product.id, this.availabilityForm.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.availability.set({
            status: 'success',
            result,
            error: null
          });
        },
        error: (error: HttpErrorResponse) => {
          this.availability.set({
            status: 'error',
            result: null,
            error: this.toErrorMessage(error, 'Nao foi possivel consultar disponibilidade.')
          });
        }
      });
  }

  protected fieldHasError(field: 'name' | 'price' | 'availableQuantity') {
    const control = this.productForm.controls[field];
    return control.invalid && (control.dirty || control.touched);
  }

  protected availabilityHasError() {
    const control = this.availabilityForm.controls.quantity;
    return control.invalid && (control.dirty || control.touched);
  }

  private toErrorMessage(error: HttpErrorResponse, fallback: string) {
    const detail = typeof error.error?.detail === 'string'
      ? error.error.detail
      : null;

    return detail ?? fallback;
  }
}
