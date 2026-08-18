import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './styles/tokens.css';
import './styles/global.css';
import './styles/layout.css';
import './styles/tables.css';
import './styles/forms.css';
import './styles/feedback.css';

const root = document.getElementById('root');

if (!root) {
  throw new Error('root_element_not_found');
}

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
