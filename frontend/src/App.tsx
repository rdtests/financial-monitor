import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { MonitorPage } from './features/monitor/MonitorPage'
import { AddPage } from './features/simulator/AddPage'
import { Layout } from './shared/Layout'

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/add" element={<AddPage />} />
          <Route path="/monitor" element={<MonitorPage />} />
          <Route path="*" element={<Navigate to="/monitor" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
