import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Stepper } from "@ship/ui";

describe("Stepper", () => {
  it("renders every step in order with their statuses", () => {
    render(
      <Stepper
        steps={[
          { code: "a", title: "Step A", status: "completed" },
          { code: "b", title: "Step B", status: "current" },
          { code: "c", title: "Step C", status: "pending" },
        ]}
      />,
    );
    const titles = screen.getAllByText(/Step [ABC]/);
    expect(titles.map((el) => el.textContent)).toEqual(["Step A", "Step B", "Step C"]);
    expect(screen.getByLabelText("Step B (current)").closest("li")?.getAttribute("aria-current")).toBe("step");
  });

  it("invokes onStepClick when a step is clicked", async () => {
    const onClick = vi.fn();
    render(
      <Stepper
        steps={[
          { code: "a", title: "Step A", status: "completed" },
          { code: "b", title: "Step B", status: "current" },
        ]}
        onStepClick={onClick}
      />,
    );
    await userEvent.click(screen.getByLabelText("Step A (completed)"));
    expect(onClick).toHaveBeenCalledWith("a");
  });
});
