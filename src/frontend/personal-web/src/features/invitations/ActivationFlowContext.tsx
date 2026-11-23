import { createContext, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";

type ActivationFlowContextValue = {
  token: string | null;
  setToken: (token: string | null) => void;
};

const ActivationFlowContext = createContext<ActivationFlowContextValue | null>(null);

export function ActivationFlowProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const value = useMemo(() => ({ token, setToken }), [token]);

  return (
    <ActivationFlowContext.Provider value={value}>
      {children}
    </ActivationFlowContext.Provider>
  );
}

export function useActivationFlow(): ActivationFlowContextValue {
  const context = useContext(ActivationFlowContext);
  if (context === null) {
    throw new Error("useActivationFlow must be used inside ActivationFlowProvider.");
  }

  return context;
}
