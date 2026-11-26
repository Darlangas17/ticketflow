import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';

import { CatalogDialog, CatalogDialogData, CatalogFormValue } from './catalog-dialog';

describe('CatalogDialog', () => {
  function setup(save: CatalogDialogData<{ id: number }>['save'], detailRequired = true) {
    const close = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: MAT_DIALOG_DATA,
          useValue: {
            title: 'New branch',
            value: { name: '', detail: '', isActive: true },
            detailLabel: 'City',
            detailRequired,
            detailMaxLength: 100,
            detailField: 'City',
            showActive: false,
            save,
          } satisfies CatalogDialogData<{ id: number }>,
        },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    const fixture = TestBed.createComponent(CatalogDialog<{ id: number }>);
    const element = fixture.nativeElement as HTMLElement;
    const type = (name: string, value: string) => {
      const input = element.querySelector<HTMLInputElement>(`input[formControlName="${name}"]`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const submit = async () => {
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      await fixture.whenStable();
    };
    return { fixture, element, close, type, submit };
  }

  it('saves trimmed values and closes with the result', async () => {
    const save = vi.fn((_: CatalogFormValue) => of({ id: 4 }));
    const { fixture, close, type, submit } = setup(save);
    await fixture.whenStable();

    type('name', ' Comayagua Plaza ');
    type('detail', ' Comayagua ');
    await submit();

    expect(save).toHaveBeenCalledWith({
      name: 'Comayagua Plaza',
      detail: 'Comayagua',
      isActive: true,
    });
    expect(close).toHaveBeenCalledWith({ id: 4 });
  });

  it('validates required fields before saving', async () => {
    const save = vi.fn(() => of({ id: 1 }));
    const { fixture, element, submit } = setup(save);
    await fixture.whenStable();

    await submit();

    expect(save).not.toHaveBeenCalled();
    expect(element.textContent).toContain('Name is required.');
    expect(element.textContent).toContain('City is required.');
  });

  it('shows API validation errors on the matching field ("City" → the detail input)', async () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { title: 'Validation', errors: { City: ["'City' must be 100 characters or fewer."] } },
    });
    const { fixture, element, close, type, submit } = setup(() => throwError(() => error));
    await fixture.whenStable();

    type('name', 'Somewhere');
    type('detail', 'Some city');
    await submit();

    expect(element.querySelector('mat-error')!.textContent).toContain('100 characters');
    expect(element.querySelector('[role="alert"]')).toBeNull();
    expect(close).not.toHaveBeenCalled();
  });
});
