import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { AddPage } from './features/simulator/AddPage'
import { Layout } from './shared/Layout'

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/add" element={<AddPage />} />
          {/* Points to /monitor once the dashboard exists (next step). */}
          <Route path="*" element={<Navigate to="/add" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
