import { useEffect, useRef } from 'react';
import { X } from 'lucide-react';

export function ConfirmDialog({ title, children, confirmLabel, busy = false, onCancel, onConfirm, danger = false }: {
  title: string; children: React.ReactNode; confirmLabel: string; busy?: boolean; onCancel: () => void; onConfirm: () => void; danger?: boolean;
}) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    cancelRef.current?.focus();
    const keydown = (event: KeyboardEvent) => { if (event.key === 'Escape' && !busy) onCancel(); if (event.key === 'Tab') { const items = dialogRef.current?.querySelectorAll<HTMLElement>('button:not([disabled])'); if (!items?.length) return; const first = items[0], last = items[items.length - 1]; if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); } else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); } } };
    window.addEventListener('keydown', keydown);
    return () => window.removeEventListener('keydown', keydown);
  }, [busy, onCancel]);
  return <div className="modal-backdrop" role="presentation"><div aria-label={title} aria-modal="true" className="confirm-dialog" ref={dialogRef} role="dialog">
    <header><h2>{title}</h2><button aria-label="关闭确认对话框" className="icon-button" onClick={onCancel} title="关闭确认对话框" type="button"><X size={17} /></button></header>
    <div className="confirm-dialog__body">{children}</div>
    <footer><button className="button button--quiet" disabled={busy} onClick={onCancel} ref={cancelRef} type="button">取消</button><button className={`button ${danger ? 'button--danger' : 'button--primary'}`} disabled={busy} onClick={onConfirm} type="button">{busy ? '正在处理' : confirmLabel}</button></footer>
  </div></div>;
}
