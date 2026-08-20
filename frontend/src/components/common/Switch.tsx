import React from 'react';

interface SwitchProps {
  id?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  label?: string;
  description?: string;
}

export const Switch: React.FC<SwitchProps> = ({
  id,
  checked,
  onChange,
  disabled = false,
  label,
  description,
}) => {
  return (
    <label
      htmlFor={id}
      className={`inline-flex items-start gap-3 select-none ${
        disabled ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'
      }`}
    >
      <div className="relative inline-flex items-center mt-0.5">
        <input
          id={id}
          type="checkbox"
          checked={checked}
          disabled={disabled}
          aria-label={label}
          onChange={(e) => onChange(e.target.checked)}
          className="sr-only peer"
        />
        <div className="w-9 h-5 bg-slate-300 peer-focus:outline-hidden peer-focus:ring-2 peer-focus:ring-blue-500 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-blue-600"></div>
      </div>
      {(label || description) && (
        <div className="flex flex-col text-sm">
          {label && <span className="font-medium text-slate-800">{label}</span>}
          {description && <span className="text-xs text-slate-500">{description}</span>}
        </div>
      )}
    </label>
  );
};
