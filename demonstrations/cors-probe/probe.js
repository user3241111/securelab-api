
document.querySelector("#probe").addEventListener("click", async () => {
  const output = document.querySelector("#result");
  const url = document.querySelector("#url").value;

  output.textContent = "";

  async function runProbe(label, options) {
    try {
      const response = await fetch(url, options);
      output.textContent += `${label}: HTTP ${response.status}\n`;
    } catch (error) {
      output.textContent += `${label}: blocked or failed (${error.name})\n`;
    }
  }

  // Ordinary credentialed GET request.
  await runProbe("GET", {
    credentials: "include"
  });

  // Custom header forces the browser to send OPTIONS preflight.
  await runProbe("Preflight GET", {
    method: "GET",
    credentials: "include",
    headers: {
      "X-Lab05-Preflight": "test"
    }
  });
});
