import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessageApiService } from './message-api.service';
import { EmployeeDto, EmployeeInstanceDto, MessageDto, MessageStatus } from './models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="wrap">
      <div class="card">
        <h1>שליחת הודעת SMS</h1>

        <label for="employee">עובד</label>
        <select id="employee" [(ngModel)]="selectedEmployeeId" (ngModelChange)="onEmployeeChange()">
          <option [ngValue]="null">-- בחר עובד --</option>
          <option *ngFor="let employee of employees" [ngValue]="employee.id">
            {{ employee.firstName }} {{ employee.lastName }} — {{ employee.address }}
          </option>
        </select>

        <div *ngIf="instances.length > 1">
          <label for="instance">מספר טלפון</label>
          <select id="instance" [(ngModel)]="selectedInstanceId">
            <option *ngFor="let instance of instances" [ngValue]="instance.id">
              {{ instance.phoneNumber }}
            </option>
          </select>
        </div>

        <label for="body">תוכן ההודעה</label>
        <textarea id="body" [(ngModel)]="body" maxlength="640"></textarea>
        <div class="hint">{{ body.length }} / 640</div>

        <button (click)="send()" [disabled]="sending || !selectedInstanceId || !body.trim()">
          {{ sending ? 'שולח...' : 'שלח' }}
        </button>

        <div class="err" *ngIf="error">{{ error }}</div>
      </div>

      <div class="card">
        <div class="row">
          <div>
            <label for="filter">סינון לפי סטטוס</label>
            <select id="filter" [(ngModel)]="filter" (ngModelChange)="load()">
              <option [ngValue]="undefined">הכל</option>
              <option *ngFor="let status of statuses" [ngValue]="status">{{ status }}</option>
            </select>
          </div>
          <div>
            <button (click)="load()">רענן</button>
          </div>
        </div>

        <p class="hint" *ngIf="total > 0">סה״כ {{ total }} הודעות</p>

        <table>
          <thead>
            <tr>
              <th>#</th>
              <th>נמען</th>
              <th>תוכן</th>
              <th>סטטוס</th>
              <th>נשלח</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let message of messages">
              <td>{{ message.id }}</td>
              <td>{{ message.to }}</td>
              <td>{{ message.body }}</td>
              <td>
                <span class="badge" [ngClass]="badgeClass(message.status)">{{ message.status }}</span>
              </td>
              <td>{{ message.createdAt | date: 'dd/MM/yy HH:mm' }}</td>
            </tr>
            <tr *ngIf="messages.length === 0">
              <td colspan="5">אין הודעות להצגה</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class AppComponent implements OnInit {
  employees: EmployeeDto[] = [];
  instances: EmployeeInstanceDto[] = [];
  messages: MessageDto[] = [];
  total = 0;

  readonly statuses: MessageStatus[] = ['Pending', 'Sending', 'Sent', 'Delivered', 'Failed'];

  selectedEmployeeId: number | null = null;
  selectedInstanceId: number | null = null;
  filter?: MessageStatus;
  body = '';
  sending = false;
  error = '';

  constructor(private readonly api: MessageApiService) {}

  ngOnInit(): void {
    this.api.employees().subscribe({
      next: employees => (this.employees = employees),
      error: () => (this.error = 'טעינת העובדים נכשלה')
    });
    this.load();
  }

  onEmployeeChange(): void {
    const employee = this.employees.find(e => e.id === this.selectedEmployeeId);
    this.instances = employee ? employee.instances : [];
    this.selectedInstanceId = this.instances.length > 0 ? this.instances[0].id : null;
  }

  send(): void {
    if (!this.selectedInstanceId) return;

    this.sending = true;
    this.error = '';

    this.api.send(this.selectedInstanceId, this.body).subscribe({
      next: () => {
        this.body = '';
        this.sending = false;
        this.load();
      },
      error: err => {
        this.sending = false;
        this.error = err?.error?.errors?.body?.[0] ?? 'שליחת ההודעה נכשלה';
      }
    });
  }

  load(): void {
    this.api.list(this.filter).subscribe({
      next: result => {
        this.messages = result.items;
        this.total = result.total;
      },
      error: () => (this.error = 'טעינת ההודעות נכשלה')
    });
  }

  badgeClass(status: MessageStatus): string {
    if (status === 'Sent' || status === 'Delivered') return 'ok';
    if (status === 'Failed') return 'bad';
    return 'wait';
  }
}
