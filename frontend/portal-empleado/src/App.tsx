import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { useEffect, useState } from 'react';
import Login from './pages/Login';
import Home from './pages/Home';
import Recibos from './pages/Recibos';
import Vacaciones from './pages/Vacaciones';
import { getDefaultFeatures, loadFeatures, type FeatureFlags } from './config/features';

function App() {
  const [features, setFeatures] = useState<FeatureFlags>(getDefaultFeatures());

  useEffect(() => {
    loadFeatures().then(setFeatures);
  }, []);

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/" element={<Home features={features} />} />
        <Route path="/recibos" element={<Recibos features={features} />} />
        {features.vacaciones
          ? <Route path="/vacaciones" element={<Vacaciones />} />
          : <Route path="/vacaciones" element={<Navigate to="/" replace />} />}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;

