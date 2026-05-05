import { describe, expect, it, beforeEach, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import ShipmentDetail from "../pages/ShipmentDetail";
import type { CustomerShipmentView } from "../types/api";

const sample: CustomerShipmentView = {
  shipmentId: "11111111-1111-1111-1111-111111111111",
  tenantId: "22222222-2222-2222-2222-222222222222",
  batchId: "33333333-3333-3333-3333-333333333333",
  status: "Labeled",
  carrierCode: "postnl",
  trackingNumber: "3SAB1234567",
  labelUri: "https://mock.postnl.local/labels/x.pdf",
  failureReason: null,
  createdAt: "2026-04-26T10:00:00Z",
  updatedAt: "2026-04-26T10:05:00Z",
  events: [
    { sequence: 0, eventCode: "Accepted", description: "Picked up", location: "Hoofddorp", occurredAt: "2026-04-26T10:01:00Z" },
    { sequence: 1, eventCode: "InTransit", description: "On route", location: "Amsterdam", occurredAt: "2026-04-26T10:30:00Z" },
  ],
};

beforeEach(() => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    status: 200,
    json: async () => sample,
    text: async () => JSON.stringify(sample),
  });
  vi.stubGlobal("fetch", fetchMock);
});

function renderRoute() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <MemoryRouter initialEntries={[`/shipments/${sample.shipmentId}`]}>
      <QueryClientProvider client={client}>
        <Routes>
          <Route path="/shipments/:shipmentId" element={<ShipmentDetail />} />
        </Routes>
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

describe("ShipmentDetail", () => {
  it("renders carrier, tracking number, label link and timeline events", async () => {
    renderRoute();
    await waitFor(() => expect(screen.getByText(/3SAB1234567/)).toBeInTheDocument());
    expect(screen.getByRole("link", { name: /Open label PDF/i })).toHaveAttribute(
      "href",
      "https://mock.postnl.local/labels/x.pdf",
    );
    expect(screen.getByText("Accepted")).toBeInTheDocument();
    expect(screen.getByText("InTransit")).toBeInTheDocument();
  });
});
