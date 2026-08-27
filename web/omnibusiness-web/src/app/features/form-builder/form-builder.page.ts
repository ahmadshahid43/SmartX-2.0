import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FormBuilder, FormCanvasField, SaveFormFieldRequest } from '../../core/models';
import { WorkspaceApiService } from '../../core/workspace-api.service';

@Component({
  selector: 'app-form-builder-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './form-builder.page.html',
  styleUrl: './form-builder.page.scss',
})
export class FormBuilderPageComponent {
  private readonly workspaceApi = inject(WorkspaceApiService);

  protected readonly formBuilder = signal<FormBuilder | null>(null);
  protected readonly selectedFieldId = signal('');
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');

  protected fieldDraft: SaveFormFieldRequest = this.createEmptyDraft();

  protected readonly groupedLibrary = computed(() => {
    const library = this.formBuilder()?.library ?? [];
    return Array.from(
      library.reduce((groups, field) => {
        const items = groups.get(field.group) ?? [];
        items.push(field);
        groups.set(field.group, items);
        return groups;
      }, new Map<string, typeof library>()),
    );
  });

  protected readonly selectedField = computed(() => {
    const canvas = this.formBuilder()?.canvas ?? [];
    return canvas.find((field) => field.fieldId === this.selectedFieldId()) ?? null;
  });

  constructor() {
    this.loadBuilder();
  }

  protected selectField(field: FormCanvasField): void {
    this.selectedFieldId.set(field.fieldId);
    this.fieldDraft = {
      label: field.label,
      type: field.type,
      required: field.required,
      placeholder: field.placeholder,
      helpText: field.helpText,
      defaultValue: field.defaultValue,
      isReadOnly: field.isReadOnly,
      minValue: field.minValue,
      maxValue: field.maxValue,
    };
  }

  protected startNewField(type = 'ShortText'): void {
    this.selectedFieldId.set('');
    this.fieldDraft = this.createEmptyDraft(type);
  }

  protected saveField(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    const isEditing = !!this.selectedField();

    const request = {
      ...this.fieldDraft,
      minValue: this.fieldDraft.minValue === null ? null : Number(this.fieldDraft.minValue),
      maxValue: this.fieldDraft.maxValue === null ? null : Number(this.fieldDraft.maxValue),
    };

    const operation = this.selectedField()
      ? this.workspaceApi.updateProductCustomField(this.selectedFieldId(), request)
      : this.workspaceApi.addProductCustomField(request);

    operation.subscribe({
      next: (builder) => {
        this.applyBuilder(builder);
        this.successMessage.set(isEditing ? 'Field updated successfully.' : 'Field created successfully.');
        this.loading.set(false);
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'Field save nahi ho saka.');
        this.loading.set(false);
      },
    });
  }

  protected deleteSelectedField(): void {
    if (!this.selectedField()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.workspaceApi.deleteProductCustomField(this.selectedFieldId()).subscribe({
      next: (builder) => {
        this.applyBuilder(builder);
        this.successMessage.set('Field deleted successfully.');
        this.loading.set(false);
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message ?? 'Field delete nahi ho saka.');
        this.loading.set(false);
      },
    });
  }

  protected useLibraryType(key: string): void {
    this.startNewField(this.mapLibraryKeyToFieldType(key));
  }

  private createEmptyDraft(type = 'ShortText'): SaveFormFieldRequest {
    return {
      label: '',
      type,
      required: false,
      placeholder: '',
      helpText: null,
      defaultValue: null,
      isReadOnly: false,
      minValue: null,
      maxValue: null,
    };
  }

  private loadBuilder(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.workspaceApi.getProductCustomFields().subscribe({
      next: (builder) => {
        this.applyBuilder(builder);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Form builder load nahi ho saka. API run aur login session check karein.');
        this.loading.set(false);
      },
    });
  }

  private applyBuilder(builder: FormBuilder): void {
    this.formBuilder.set(builder);
    const nextField = builder.canvas.find((field) => field.fieldId === builder.selectedFieldId) ?? builder.canvas[0] ?? null;

    if (nextField) {
      this.selectField(nextField);
      return;
    }

    this.startNewField();
  }

  private mapLibraryKeyToFieldType(key: string): string {
    const mapping: Record<string, string> = {
      'short-text': 'ShortText',
      'long-text': 'LongText',
      number: 'Number',
      date: 'Date',
      dropdown: 'Dropdown',
      formula: 'Formula',
      lookup: 'Lookup',
    };

    return mapping[key] ?? 'ShortText';
  }
}
