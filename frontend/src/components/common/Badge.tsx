import React from 'react';

interface BadgeProps {
  label: string;
  badgeClass?: string;
  dotClass?: string;
  subText?: string;
  icon?: React.ReactNode;
  className?: string;
  title?: string;
}

export const Badge: React.FC<BadgeProps> = ({
  label,
  badgeClass = 'bg-slate-100 text-slate-700 border-slate-200',
  dotClass,
  subText,
  icon,
  className = '',
  title,
}) => {
  return (
    <span
      title={title}
      className={`inline-flex items-center gap-1.5 px-2 py-0.5 text-xs font-medium rounded border ${badgeClass} ${className}`}
    >
      {dotClass && <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${dotClass}`} />}
      {icon && <span className="shrink-0">{icon}</span>}
      <span className="truncate">{label}</span>
      {subText && <span className="text-[10px] opacity-75 font-mono">({subText})</span>}
    </span>
  );
};
