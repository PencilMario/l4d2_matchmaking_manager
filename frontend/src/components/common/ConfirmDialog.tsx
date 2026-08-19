import React from 'react';
import { AlertTriangle, Info, AlertCircle, Loader2 } from 'lucide-react';
import { Modal } from './Modal';

interface ConfirmDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  description: string | React.ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  intent?: 'danger' | 'warning' | 'info';
  isLoading?: boolean;
}

export const ConfirmDialog: React.FC<ConfirmDialogProps> = ({
  isOpen,
  onClose,
  onConfirm,
  title,
  description,
  confirmLabel = '确认',
  cancelLabel = '取消',
  intent = 'danger',
  isLoading = false,
}) => {
  let icon = <AlertTriangle className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />;
  let btnClass = 'bg-red-600 hover:bg-red-700 text-white focus:ring-red-500';

  if (intent === 'warning') {
    icon = <AlertCircle className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />;
    btnClass = 'bg-amber-600 hover:bg-amber-700 text-white focus:ring-amber-500';
  } else if (intent === 'info') {
    icon = <Info className="w-5 h-5 text-blue-600 shrink-0 mt-0.5" />;
    btnClass = 'bg-blue-600 hover:bg-blue-700 text-white focus:ring-blue-500';
  }

  const footer = (
    <>
      <button
        type="button"
        disabled={isLoading}
        onClick={onClose}
        className="px-3.5 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50 focus:outline-hidden focus:ring-2 focus:ring-slate-400 disabled:opacity-50 transition-colors"
      >
        {cancelLabel}
      </button>
      <button
        type="button"
        disabled={isLoading}
        onClick={onConfirm}
        className={`inline-flex items-center gap-1.5 px-4 py-1.5 text-xs font-medium rounded-md shadow-xs focus:outline-hidden focus:ring-2 disabled:opacity-50 transition-colors ${btnClass}`}
      >
        {isLoading && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
        {confirmLabel}
      </button>
    </>
  );

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title} footer={footer} maxWidth="max-w-md">
      <div className="flex items-start gap-3 py-1">
        {icon}
        <div className="text-sm text-slate-700 leading-relaxed">{description}</div>
      </div>
    </Modal>
  );
};
