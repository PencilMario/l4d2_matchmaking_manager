import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import App from './App';
describe('登录', () => { it('空令牌不能提交且支持显示令牌', () => { render(<App />); const button = screen.getByRole('button', { name: '进入管理后台' }); expect(button).toBeDisabled(); fireEvent.click(screen.getByRole('button', { name: '显示令牌' })); expect(screen.getByLabelText('访问令牌')).toHaveAttribute('type', 'text'); }); });
