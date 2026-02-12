# ARCoatingDesigner User Guide

A detailed guide for optical engineers covering all features of the AR Coating Designer application.

---

## Table of Contents

1. [Getting Started](#1-getting-started)
2. [Design Basics](#2-design-basics)
3. [Layer Management](#3-layer-management)
4. [Coating Materials](#4-coating-materials)
5. [Merit Targets](#5-merit-targets)
6. [Optimization](#6-optimization)
7. [Plots](#7-plots)
8. [Catalogs](#8-catalogs)
9. [Export](#9-export)
10. [Keyboard & UI Tips](#10-keyboard--ui-tips)

---

## 1. Getting Started

### Install .NET 8.0

Download and install the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) for your platform (Windows, Linux, or macOS).

### Clone and Build

```bash
git clone <repository-url>
cd ARCoatingDesigner
dotnet build
```

### Run

```bash
dotnet run --project src/ARCoatingDesigner.App
```

On launch, the application:
- Loads all glass catalogs from `catalogs/Glass/` (SCHOTT, HOYA, OHARA, CDGM, SUMITA)
- Loads the default coating catalog from `catalogs/Coatings/StandardCoatings.coat`
- Initializes the 7 built-in coating materials
- Opens with a default SLAR (single-layer AR) design on N-BK7

---

## 2. Design Basics

### Creating a New Design

Use the **Design** menu to start from a template:

| Template | Layers | Description |
|----------|--------|-------------|
| **SLAR** | 1 (MgF2 QWOT) | Single-layer anti-reflection. Targets: Rave = 0 across 0.45-0.65 um. |
| **V-Coat** | 2 (MgF2 + TiO2) | Two-layer V-coat for near-zero reflectance at 0.55 um. MgF2 (outer, 0.3239 waves) / TiO2 (inner, 0.0502 waves) on N-BK7. |
| **Empty** | 0 | Blank design on N-BK7 substrate. Add layers and targets manually. |

You can also load a saved design from the coating catalog (see [Catalogs](#8-catalogs)).

### Substrate Selection

Choose the substrate glass from the glass catalog dropdown. The default is **N-BK7** (n ~ 1.517 at 550 nm). Filter by manufacturer using the catalog selector (SCHOTT, HOYA, OHARA, CDGM, SUMITA, or All).

The substrate refractive index is computed from the glass catalog's Sellmeier dispersion data at each wavelength in the calculation.

### Reference Wavelength

The reference wavelength (default 0.55 um) is used for:
- Converting between optical and physical thickness
- Quarter-wave optical thickness (QWOT) definitions

### Optical vs Physical Thickness

The application supports two thickness modes:

- **Optical thickness** (waves): thickness expressed as a fraction of the reference wavelength. A quarter-wave layer is 0.25.
- **Physical thickness** (um): actual layer thickness in microns.

The conversion formulas are:

```
Optical = n * Physical / ReferenceWavelength
Physical = Optical * ReferenceWavelength / n
```

where `n` is the layer material's refractive index at the reference wavelength.

Toggling between modes converts all layer thicknesses and their min/max bounds automatically.

---

## 3. Layer Management

### Adding Layers

Click **Add Layer** to append a new layer. Defaults:
- Material: first available (typically MgF2)
- Thickness: 0.25 waves (optical) or 0.1 um (physical)
- Variable: checked (included in optimization)
- Min/Max bounds: 10% to 10x of the initial thickness

### Layer Order

Layers are ordered from the air interface inward: **layer 1 is closest to air (outermost)**, and the last layer is closest to the substrate (innermost). This matches the transfer matrix multiplication order and ZEMAX convention. Use the **Up/Down** buttons to reorder layers.

### Layer Properties

| Property | Description |
|----------|-------------|
| **Material** | Select from available coating materials dropdown |
| **Thickness** | Layer thickness in the current mode (optical waves or physical um) |
| **Variable** | Checkbox: include this layer in optimization |
| **Min** | Lower bound for optimization |
| **Max** | Upper bound for optimization |

### Removing Layers

Select a layer and click **Remove Layer** to delete it.

---

## 4. Coating Materials

### Built-in Materials

Seven common thin film materials are available by default:

#### Sellmeier Materials

| Material | n (550 nm) | B1 | C1 | B2 | C2 | B3 | C3 |
|----------|-----------|-----|------|------|--------|---------|---------|
| MgF2 | ~1.38 | 0.48755108 | 0.001882178 | 0.39875031 | 0.008951888 | 2.3120353 | 566.13559 |
| SiO2 | ~1.46 | 0.6961663 | 0.0046791 | 0.4079426 | 0.0135121 | 0.8974794 | 97.9340 |
| Al2O3 | ~1.77 | 1.4313493 | 0.0052799 | 0.65054713 | 0.0142383 | 5.3414021 | 325.01783 |

#### Cauchy Materials

| Material | n (550 nm) | A | B | C |
|----------|-----------|------|-------|-------|
| TiO2 | ~2.35 | 2.20 | 0.030 | 0.003 |
| Ta2O5 | ~2.10 | 1.97 | 0.022 | 0.002 |
| ZrO2 | ~2.05 | 1.92 | 0.022 | 0.002 |
| HfO2 | ~1.95 | 1.84 | 0.018 | 0.002 |

### Adding Custom Materials

Use the **Add Material** dialog to define a new coating material. Choose one of:

- **Constant**: a fixed refractive index and extinction coefficient (no wavelength dependence)
- **Cauchy**: specify A, B, C coefficients
- **Sellmeier**: specify B1, C1, B2, C2, B3, C3 coefficients

Custom materials are added to the current session's material list and can be saved to a coating catalog.

### Dispersion Formulas

**Cauchy:**

```
n(lambda) = A + B / lambda^2 + C / lambda^4
```

where lambda is in microns.

**Sellmeier (standard, A = 1):**

```
n^2 = 1 + B1*lambda^2/(lambda^2 - C1) + B2*lambda^2/(lambda^2 - C2) + B3*lambda^2/(lambda^2 - C3)
```

where lambda is in microns. The modified Sellmeier form replaces the constant 1 with a custom value A.

**Complex refractive index convention (matches ZEMAX):**

```
n_complex = n + ik
```

For non-absorbing dielectrics, k = 0. For absorbing materials (metals), k < 0.

---

## 5. Merit Targets

### Target Types

Each target specifies a performance metric to optimize at a particular wavelength and angle of incidence:

| Type | Description |
|------|-------------|
| **Rs** | S-polarized reflectance (%) |
| **Rp** | P-polarized reflectance (%) |
| **Rave** | Average reflectance = (Rs + Rp) / 2 |
| **Ts** | S-polarized transmittance (%) |
| **Tp** | P-polarized transmittance (%) |
| **Tave** | Average transmittance = (Ts + Tp) / 2 |

### Compare Types

| Compare | Merit contribution |
|---------|-------------------|
| **= (Equal)** | weight * (calculated - target)^2 |
| **<= (LessOrEqual)** | weight * max(0, calculated - target)^2 |
| **>= (GreaterOrEqual)** | weight * max(0, target - calculated)^2 |

The "less or equal" and "greater or equal" comparisons are one-sided: they contribute zero merit when the constraint is satisfied, and a squared-error penalty when violated.

### Weights

Each target has a weight that scales its contribution to the total merit function. Use higher weights on targets that are more important to the design.

### Target Generator

The **Generate Targets** dialog creates a grid of targets across wavelength and angle ranges:

- **Wavelength range**: min, max, and step (in um)
- **Angle range**: min, max, and step (in degrees)
- **Target type**: Rs, Rp, Rave, Ts, Tp, or Tave
- **Target value**: desired performance (e.g., 0 for zero reflectance)
- **Compare type**: =, <=, or >=
- **Weight**: applied to all generated targets

Example: to target Rave = 0 across 0.45-0.65 um at normal incidence, set wavelength min=0.45, max=0.65, step=0.005, AOI min=0, max=0, target=0, compare=Equal.

### Total Merit Function

The optimizer minimizes the total merit, which is the weighted sum of squared errors across all active targets:

```
Merit = SUM[ weight_i * error_i^2 ]
```

where the error for each target depends on the compare type (see above). Lower merit means better performance. Disabled targets (unchecked) contribute zero.

---

## 6. Optimization

### Local Optimization (Levenberg-Marquardt)

Click **Optimize (Local)** to run damped least-squares optimization on the current design.

**How it works:**
1. Computes the Jacobian matrix via finite differences (delta = 1e-7)
2. Solves the normal equations with adaptive damping: `(J^T*J + mu*diag(J^T*J)) * step = -J^T*r`
3. If the step reduces merit, accept and decrease damping (mu *= 0.333)
4. If the step increases merit, reject and increase damping (mu *= 3.0)
5. Clamps layer thicknesses to their [Min, Max] bounds at each step

**Parameters:**
| Parameter | Default | Description |
|-----------|---------|-------------|
| Max iterations | 200 | Maximum LM iterations |
| Initial mu | 1e-3 | Starting damping factor |
| Gradient tolerance | 1e-10 | Stop when gradient norm is below this |
| Step tolerance | 1e-10 | Stop when step size is below this |
| Function tolerance | 1e-10 | Stop when merit change is below this |

### Global Optimization (Multi-Trial)

Click **Optimize (Global)** to run a multi-start global search. This is useful when the local optimizer gets trapped in a local minimum.

**How it works:**
1. For each trial, generate random starting thicknesses within the [Min, Max] bounds
2. Run a full Levenberg-Marquardt optimization from that starting point
3. Keep the best result across all trials
4. Report progress (trial number and best merit) in real time

**Parameters:**
| Parameter | Default | Description |
|-----------|---------|-------------|
| Number of trials | 1000 | Total random restarts |
| Max iterations per trial | 200 | LM iterations per trial |

**Stopping:** Click **Stop** to cancel the global optimization early. The best result found so far is applied to the design.

---

## 7. Plots

### R vs Wavelength

The default plot mode. Shows reflectance (%) as a function of wavelength at a fixed angle of incidence.

- **Wavelength range**: adjustable (default 0.4 - 0.8 um)
- **AOI**: fixed angle of incidence (default 0 degrees)
- **Traces**: Rs (s-polarization), Rp (p-polarization), Rave (average)
- **Y-axis range**: adjustable (default 0 - 10%)

### R vs Angle

Toggle to angular response mode. Shows reflectance (%) as a function of angle of incidence at a fixed wavelength.

- **Angle range**: adjustable (default 0 - 80 degrees)
- **Wavelength**: fixed wavelength for the calculation (default 0.55 um)
- **Traces**: Rs, Rp, Rave

### Index Plot (n, k vs Wavelength)

Open the **Index Plot** dialog to view the refractive index and extinction coefficient of any material or glass as a function of wavelength.

- **Source**: choose a coating material or a glass from the catalog
- **Wavelength range**: configurable
- **Axes**: n on the left axis, k on the right axis

The index data can be exported as a tab-delimited text file (wavelength, n, k).

---

## 8. Catalogs

### Coating Catalogs (.coat files)

Coating catalogs store material definitions and coating designs in a text-based format.

**Loading a catalog:** Use **Catalog > Open Catalog** to load a `.coat` file. This replaces the current material list with the catalog's materials.

**Saving a catalog:** Use **Catalog > Save Catalog** (overwrites the current file) or **Catalog > Save Catalog As** (saves to a new location).

**Catalog file format:**
```
MATERIAL <Name> <Model> <Coefficients...>
COATING <Name> <ReferenceWavelength>
  LAYER <Material> <Thickness> <O|P>
```
- `O` = optical thickness (waves)
- `P` = physical thickness (um)

### Saving and Loading Designs

**Save to catalog:** Use **Catalog > Save Design to Catalog** to store the current design in the active catalog. The design is held in memory until you save the catalog file.

**Load from catalog:** Use **Catalog > Load Design from Catalog** to select a previously saved coating design. The application loads the layer stack and generates default merit targets (Rave <= 0 across 0.45-0.65 um).

### Glass Catalogs (AGF Format)

Glass catalogs use the ZEMAX AGF format and provide Sellmeier dispersion data for substrate glasses.

**Included catalogs** (in `catalogs/Glass/`):
| Catalog | Manufacturer |
|---------|-------------|
| SCHOTT.AGF | Schott AG (Germany) |
| HOYA.AGF | Hoya Corporation (Japan) |
| OHARA.AGF | Ohara Inc. (Japan) |
| CDGM.AGF | CDGM Glass Co. (China) |
| SUMITA.AGF | Sumita Optical Glass (Japan) |

**Catalog selector:** Use the catalog dropdown to filter the glass list by manufacturer, or select **(All)** to see all loaded glasses (limited to 500 entries for UI performance).

**Loading additional catalogs:** Use **Catalog > Load Glass Catalog** to load additional AGF files.

---

## 9. Export

### ZEMAX .dat Export

Use **Export > ZEMAX (.dat)** to export the entire coating catalog (all materials and all coating designs) as a ZEMAX-compatible file. A Save dialog lets you choose the folder and filename; the default name is derived from the catalog file.

**File structure:**
```
! ZEMAX Coating File
! Generated from ARCoatingDesigner

MATE MGF2
  0.3500 1.384000 0.000000
  0.3600 1.383500 0.000000
  ...                          (41 points, 0.35-0.75 um, 0.01 um step)

COAT SLAR
MGF2 0.25

COAT VCOAT
MGF2 0.3239
TIO2 0.0502

COAT TESTBBAR
SIO2 0.0517 1
TIO2 0.016 1
```

- All catalog materials are exported as `MATE` entries with n-k tables (41 wavelength points, 0.35-0.75 um)
- All catalog coatings are exported as `COAT` entries
- Optical thickness layers: `MATERIAL thickness` (no flag)
- Physical thickness layers: `MATERIAL thickness 1`
- Material names are sanitized: uppercase, no spaces, max 16 characters

### Text Export

Use **Export > Text** to save a tab-delimited data file with a design header and spectral or angular data.

**Header:**
```
# AR Coating Designer Export
# Coating: SLAR
# Substrate: N-BK7
# Reference Wavelength: 0.5500 um
# Layers: 1
#   Layer 1: MgF2  T=0.2500
# Merit: 1.234567
```

**Data columns (R vs Wavelength mode):**
```
Wavelength_um    Rs    Rp    Rave
```

**Data columns (R vs Angle mode):**
```
AOI_deg    Rs    Rp    Rave
```

---

## 10. Keyboard & UI Tips

| Action | How |
|--------|-----|
| Toggle optical/physical thickness | Check/uncheck the **Optical Thickness** checkbox. A dialog asks whether to convert existing thicknesses. |
| Refresh the plot | Change any design parameter, or re-select the current plot mode |
| Stop global optimization | Click the **Stop** button (enabled during global optimization) |
| Switch plot mode | Toggle between **R vs Wavelength** and **R vs Angle** radio buttons |
| View material dispersion | Open the **Index Plot** dialog from the menu |
| Filter glasses by catalog | Use the catalog dropdown above the glass list |

### Workflow Tips

1. **Start with a template** (SLAR or V-Coat) to get a working design quickly.
2. **Generate targets** across your desired wavelength range before optimizing.
3. **Run local optimization** first to refine the current design.
4. **Set sensible Min/Max bounds** on each layer to keep the optimizer in a physically meaningful range.
5. **Use global optimization** when the local optimizer converges to a poor minimum. This is most useful for designs with 3+ layers where many local minima exist.
6. **Save your design to the catalog** before experimenting with different configurations, so you can easily return to a known-good state.
