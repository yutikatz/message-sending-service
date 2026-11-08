export type MessageStatus =
  | 'Pending'
  | 'Sending'
  | 'Sent'
  | 'Delivered'
  | 'Failed';

export interface EmployeeInstanceDto {
  id: number;
  phoneNumber: string;
}

export interface EmployeeDto {
  id: number;
  firstName: string;
  lastName: string;
  address: string;
  instances: EmployeeInstanceDto[];
}

export interface MessageDto {
  id: number;
  to: string;
  body: string;
  status: MessageStatus;
  error?: string | null;
  createdAt: string;
  sentAt?: string | null;
}

export interface MessageListDto {
  page: number;
  pageSize: number;
  total: number;
  items: MessageDto[];
}

export interface SendResultDto {
  id: number;
  status: string;
}
