import { useEffect, useRef, type CSSProperties, type MouseEventHandler, type ReactNode } from 'react';
import './SpecularButton.css';

type ButtonSize = 'sm' | 'md' | 'lg';

export interface SpecularButtonProps {
  children?: ReactNode;
  size?: ButtonSize;
  radius?: number;
  tint?: string;
  tintOpacity?: number;
  textColor?: string;
  lineColor?: string;
  baseColor?: string;
  disabled?: boolean;
  onClick?: MouseEventHandler<HTMLButtonElement>;
  className?: string;
  type?: 'button' | 'submit' | 'reset';
}

export default function SpecularButton({
  children,
  size = 'md',
  radius = 4,
  tint = '#b4dd59',
  tintOpacity = 0.12,
  textColor = '#edf4ed',
  lineColor = '#b4dd59',
  baseColor = '#16221d',
  disabled = false,
  onClick,
  className = '',
  type = 'button',
}: SpecularButtonProps) {
  const buttonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const button = buttonRef.current;
    if (!button) return;
    let frame = 0;
    const update = (event: PointerEvent) => {
      const rect = button.getBoundingClientRect();
      const x = ((event.clientX - rect.left) / Math.max(rect.width, 1)) * 100;
      const y = ((event.clientY - rect.top) / Math.max(rect.height, 1)) * 100;
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        button.style.setProperty('--sb-x', String(Math.max(0, Math.min(100, x))) + '%');
        button.style.setProperty('--sb-y', String(Math.max(0, Math.min(100, y))) + '%');
      });
    };
    button.addEventListener('pointermove', update, { passive: true });
    return () => {
      cancelAnimationFrame(frame);
      button.removeEventListener('pointermove', update);
    };
  }, []);

  return (
    <button
      ref={buttonRef}
      className={'specular-button specular-button--' + size + (className ? ' ' + className : '')}
      disabled={disabled}
      onClick={onClick}
      style={{ '--sb-radius': String(radius) + 'px', '--sb-tint': tint, '--sb-tint-opacity': tintOpacity, '--sb-line': lineColor, '--sb-base': baseColor, '--sb-text': textColor } as CSSProperties}
      type={type}
    >
      <span aria-hidden="true" className="specular-button__shine" />
      <span className="specular-button__label">{children}</span>
    </button>
  );
}
