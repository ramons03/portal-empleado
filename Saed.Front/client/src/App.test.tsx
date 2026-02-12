import React from 'react';
import { render, screen } from '@testing-library/react';
import App from './App';

test('renders Saed Security PoC heading', () => {
  render(<App />);
  const headingElement = screen.getByText(/Saed Security PoC/i);
  expect(headingElement).toBeInTheDocument();
});
