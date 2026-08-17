import { lazy, Suspense, useEffect, useState } from 'react';

const Hyperspeed = lazy(() => import('../../components/react-bits/Hyperspeed/Hyperspeed'));

const effectOptions = {
  distortion: 'turbulentDistortion', speedUp: 0.3, fov: 82, fovSpeedUp: 95,
  colors: { roadColor: 0x07110f, islandColor: 0x0d1715, background: 0x060908, shoulderLines: 0x58d1d7, brokenLines: 0xb4dd59, leftCars: [0x2d5c5e, 0x334a2d], rightCars: [0x58d1d7, 0xb4dd59], sticks: 0x58d1d7 },
};

export function VisualStage() {
  const [webglEnabled, setWebglEnabled] = useState(false);
  useEffect(() => {
    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
    setWebglEnabled(typeof WebGLRenderingContext !== 'undefined' && !reduceMotion);
  }, []);
  return <div className="visual-stage" data-visual-stage aria-hidden="true">{webglEnabled ? <Suspense fallback={<div className="visual-stage__fallback" />}><Hyperspeed effectOptions={effectOptions} /></Suspense> : <div className="visual-stage__fallback" />}</div>;
}
