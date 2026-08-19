import React, { useState, useRef, useEffect } from 'react';

interface TooltipProps {
  content: string | React.ReactNode;
  children: React.ReactNode;
  position?: 'top' | 'bottom' | 'left' | 'right';
  className?: string;
}

export const Tooltip: React.FC<TooltipProps> = ({
  content,
  children,
  position = 'top',
  className = '',
}) => {
  const [visible, setVisible] = useState(false);
  const timeoutRef = useRef<NodeJS.Timeout | null>(null);

  const show = () => {
    timeoutRef.current = setTimeout(() => {
      setVisible(true);
    }, 150);
  };

  const hide = () => {
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
    }
    setVisible(false);
  };

  useEffect(() => {
    return () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
    };
  }, []);

  let posClasses = 'bottom-full left-1/2 -translate-x-1/2 mb-1.5';
  if (position === 'bottom') posClasses = 'top-full left-1/2 -translate-x-1/2 mt-1.5';
  if (position === 'left') posClasses = 'right-full top-1/2 -translate-y-1/2 mr-1.5';
  if (position === 'right') posClasses = 'left-full top-1/2 -translate-y-1/2 ml-1.5';

  return (
    <div
      className={`relative inline-flex items-center ${className}`}
      onMouseEnter={show}
      onMouseLeave={hide}
      onFocus={show}
      onBlur={hide}
    >
      {children}
      {visible && content && (
        <div
          role="tooltip"
          className={`absolute z-50 px-2 py-1 text-xs font-normal text-slate-100 bg-slate-800 rounded shadow-md whitespace-nowrap pointer-events-none transition-opacity duration-150 ${posClasses}`}
        >
          {content}
        </div>
      )}
    </div>
  );
};
