import { render, screen } from '@testing-library/react';
import App from './App';

describe('App', () => {
  it('renders the management workspace bootstrap', () => {
    render(<App />);

    expect(screen.getByRole('heading', { name: 'L4D2 Matchmaking Manager' })).toBeInTheDocument();
    expect(screen.getByText('管理工作区')).toBeInTheDocument();
  });
});
